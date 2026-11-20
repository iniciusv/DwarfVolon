using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using DwarfVolon.DFHack.Protocol;

namespace DwarfVolon.DFHack;

/// <summary>Resultado de um comando do DFHack (enum <c>command_result</c> do C++).</summary>
public enum CommandResultCode
{
    LinkFailure = -3,
    NeedsConsole = -2,
    NotImplemented = -1,
    Ok = 0,
    Failure = 1,
    WrongUsage = 2,
    NotFound = 3,
}

/// <summary>Saída de texto e código de resultado de um comando.</summary>
public sealed record CommandResult(CommandResultCode Code, string Output)
{
    public bool Success => Code == CommandResultCode.Ok;
}

public class DfHackException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Cliente do servidor RPC do DFHack (TCP, normalmente 127.0.0.1:5000).
/// Protocolo: handshake "DFHack?\n"/"DFHack!\n" + versão; depois mensagens com
/// cabeçalho { int16 id, int16 padding, int32 tamanho } + corpo protobuf.
/// Uma chamada por vez (as chamadas são serializadas internamente).
/// </summary>
public sealed class DfHackClient : IAsyncDisposable, IDisposable
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 5000;

    private const short IdBindMethod = 0;
    private const short IdRunCommand = 1;
    private const short ReplyResult = -1;
    private const short ReplyFail = -2;
    private const short ReplyText = -3;
    private const short RequestQuit = -4;
    private const int MaxMessageSize = 64 * 1024 * 1024;

    private readonly TcpClient _tcp;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, short> _boundMethods = [];
    private bool _disposed;

    private DfHackClient(TcpClient tcp)
    {
        _tcp = tcp;
        _stream = tcp.GetStream();
    }

    public bool IsConnected => !_disposed && _tcp.Connected;

    public static async Task<DfHackClient> ConnectAsync(
        string host = DefaultHost, int port = DefaultPort,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var tcp = new TcpClient { NoDelay = true };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(3));
        try
        {
            await tcp.ConnectAsync(host, port, cts.Token);
            var client = new DfHackClient(tcp);
            await client.HandshakeAsync(cts.Token);
            return client;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            tcp.Dispose();
            if (ct.IsCancellationRequested) throw;
            throw new DfHackException(
                $"Não foi possível conectar ao DFHack em {host}:{port}. O jogo está aberto com o DFHack?", ex);
        }
    }

    private async Task HandshakeAsync(CancellationToken ct)
    {
        var hello = new byte[12];
        Encoding.ASCII.GetBytes("DFHack?\n").CopyTo(hello, 0);
        BinaryPrimitives.WriteInt32LittleEndian(hello.AsSpan(8), 1);
        await _stream.WriteAsync(hello, ct);

        var reply = new byte[12];
        await _stream.ReadExactlyAsync(reply, ct);
        if (Encoding.ASCII.GetString(reply, 0, 8) != "DFHack!\n")
            throw new DfHackException("O servidor na porta não respondeu como DFHack.");
    }

    /// <summary>Executa um comando do console do DFHack (como o <c>dfhack-run</c>).</summary>
    public async Task<CommandResult> RunCommandAsync(
        string command, IEnumerable<string>? arguments = null, CancellationToken ct = default)
    {
        var request = new ProtoWriter().String(1, command);
        foreach (var arg in arguments ?? []) request.String(2, arg);

        var (code, _, text) = await CallAsync(IdRunCommand, request.ToArray(), ct);
        return new CommandResult(code, text);
    }

    /// <summary>Executa um trecho de código Lua no DFHack e devolve o que ele imprimir.</summary>
    public Task<CommandResult> RunLuaAsync(string code, CancellationToken ct = default) =>
        RunCommandAsync("lua", [code], ct);

    /// <summary>Versão do DFHack (ex.: "53.17-r1").</summary>
    public Task<string> GetDfHackVersionAsync(CancellationToken ct = default) =>
        CallStringMethodAsync("GetVersion", ct);

    /// <summary>Versão do Dwarf Fortress (ex.: "0.53.17").</summary>
    public Task<string> GetDfVersionAsync(CancellationToken ct = default) =>
        CallStringMethodAsync("GetDFVersion", ct);

    private async Task<string> CallStringMethodAsync(string method, CancellationToken ct)
    {
        var id = await BindMethodAsync(method, "dfproto.EmptyMessage", "dfproto.StringMessage", null, ct);
        var (code, body, _) = await CallAsync(id, [], ct);
        if (code != CommandResultCode.Ok) throw new DfHackException($"{method} falhou ({code}).");
        foreach (var f in ProtoReader.Read(body))
            if (f.Number == 1) return f.AsString();
        return "";
    }

    /// <summary>Liga um método RPC pelo nome e devolve o id a usar nas chamadas.</summary>
    public async Task<short> BindMethodAsync(
        string method, string inputMessage, string outputMessage, string? plugin = null, CancellationToken ct = default)
    {
        var key = $"{plugin}:{method}";
        if (_boundMethods.TryGetValue(key, out var cached)) return cached;

        var request = new ProtoWriter()
            .String(1, method).String(2, inputMessage).String(3, outputMessage).String(4, plugin)
            .ToArray();
        var (code, body, _) = await CallAsync(IdBindMethod, request, ct);
        if (code != CommandResultCode.Ok)
            throw new DfHackException($"O DFHack não conhece o método {method} ({code}).");

        foreach (var f in ProtoReader.Read(body))
        {
            if (f.Number != 1) continue;
            var id = (short)f.AsInt64();
            _boundMethods[key] = id;
            return id;
        }
        throw new DfHackException($"Resposta inválida ao ligar {method}.");
    }

    private async Task<(CommandResultCode Code, byte[] Body, string Text)> CallAsync(
        short id, byte[] request, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lock.WaitAsync(ct);
        try
        {
            await WriteMessageAsync(id, request, ct);
            var text = new StringBuilder();
            var header = new byte[8];
            while (true)
            {
                await _stream.ReadExactlyAsync(header, ct);
                var replyId = BinaryPrimitives.ReadInt16LittleEndian(header);
                var size = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));

                if (replyId == ReplyFail)
                    return ((CommandResultCode)size, [], text.ToString());

                if (size < 0 || size > MaxMessageSize)
                    throw new DfHackException($"Mensagem do DFHack com tamanho inválido ({size}).");
                var body = new byte[size];
                await _stream.ReadExactlyAsync(body, ct);

                switch (replyId)
                {
                    case ReplyText:
                        AppendText(body, text);
                        break;
                    case ReplyResult:
                        return (CommandResultCode.Ok, body, text.ToString());
                    default:
                        throw new DfHackException($"Resposta inesperada do DFHack (id {replyId}).");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or EndOfStreamException)
        {
            _disposed = true;
            _tcp.Dispose();
            throw new DfHackException("A conexão com o DFHack caiu.", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    // CoreTextNotification { repeated CoreTextFragment fragments = 1 }
    // CoreTextFragment { required string text = 1; optional Color color = 2 }
    private static void AppendText(byte[] body, StringBuilder text)
    {
        foreach (var fragment in ProtoReader.Read(body))
        {
            if (fragment.Number != 1 || fragment.WireType != 2) continue;
            foreach (var f in ProtoReader.Read(fragment.Bytes))
                if (f.Number == 1 && f.WireType == 2) text.Append(f.AsString());
        }
    }

    private async Task WriteMessageAsync(short id, byte[] body, CancellationToken ct)
    {
        var message = new byte[8 + body.Length];
        BinaryPrimitives.WriteInt16LittleEndian(message, id);
        BinaryPrimitives.WriteInt32LittleEndian(message.AsSpan(4), body.Length);
        body.CopyTo(message, 8);
        await _stream.WriteAsync(message, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try
        {
            await _lock.WaitAsync(TimeSpan.FromSeconds(1));
            var quit = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(quit, RequestQuit);
            await _stream.WriteAsync(quit).AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Encerrando de qualquer jeito.
        }
        _disposed = true;
        _tcp.Dispose();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
