using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DwarfVolon.DFHack;
using DwarfVolon.DFHack.Protocol;

namespace DwarfVolon.Core.Tests;

/// <summary>Testa o cliente RPC contra um servidor DFHack falso (mesmo protocolo, em memória).</summary>
public sealed class DfHackClientTests
{
    [Fact]
    public void Proto_round_trip()
    {
        var bytes = new ProtoWriter().String(1, "lua").String(2, "print('ã')").Int(3, 300).Int(4, -1).ToArray();
        var fields = ProtoReader.Read(bytes).ToList();

        Assert.Equal("lua", fields[0].AsString());
        Assert.Equal("print('ã')", fields[1].AsString());
        Assert.Equal(300, fields[2].AsInt64());
        Assert.Equal(-1, fields[3].AsInt64());
    }

    [Fact]
    public async Task RunCommand_collects_text_and_result()
    {
        await using var server = new FakeDfHackServer(async (id, body, reply) =>
        {
            Assert.Equal(1, id); // RunCommand
            var fields = ProtoReader.Read(body).ToList();
            Assert.Equal("lua", fields.Single(f => f.Number == 1).AsString());
            Assert.Equal("print(1)", fields.Single(f => f.Number == 2).AsString());

            await reply.Text("olá ");
            await reply.Text("mundo\n");
            await reply.Result([]);
        });

        await using var client = await DfHackClient.ConnectAsync(port: server.Port);
        var result = await client.RunLuaAsync("print(1)");

        Assert.True(result.Success);
        Assert.Equal("olá mundo\n", result.Output);
    }

    [Fact]
    public async Task RunCommand_failure_returns_code()
    {
        await using var server = new FakeDfHackServer((_, _, reply) => reply.Fail(3));
        await using var client = await DfHackClient.ConnectAsync(port: server.Port);

        var result = await client.RunCommandAsync("nao-existe");
        Assert.Equal(CommandResultCode.NotFound, result.Code);
    }

    [Fact]
    public async Task GetVersion_binds_method_then_calls_it()
    {
        await using var server = new FakeDfHackServer(async (id, body, reply) =>
        {
            if (id == 0) // BindMethod
            {
                var fields = ProtoReader.Read(body).ToList();
                Assert.Equal("GetVersion", fields.Single(f => f.Number == 1).AsString());
                await reply.Result(new ProtoWriter().Int(1, 42).ToArray());
            }
            else
            {
                Assert.Equal(42, id);
                await reply.Result(new ProtoWriter().String(1, "53.17-r1").ToArray());
            }
        });
        await using var client = await DfHackClient.ConnectAsync(port: server.Port);

        Assert.Equal("53.17-r1", await client.GetDfHackVersionAsync());
    }

    [Fact]
    public async Task Connect_to_closed_port_gives_friendly_error()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var ex = await Assert.ThrowsAsync<DfHackException>(() => DfHackClient.ConnectAsync(port: port, timeout: TimeSpan.FromSeconds(2)));
        Assert.Contains("DFHack", ex.Message);
    }

    [Fact]
    public void Endpoint_reads_port_from_remote_server_json()
    {
        var dir = Directory.CreateTempSubdirectory("dwarfvolon-dfhack").FullName;
        try
        {
            var file = Path.Combine(dir, "remote-server.json");
            File.WriteAllText(file, "{ \"allow_remote\": false, \"port\": 5123 }");
            Assert.Equal(5123, DfHackEndpoint.ReadPort(file));
            File.WriteAllText(file, "não é json");
            Assert.Null(DfHackEndpoint.ReadPort(file));
            Assert.Null(DfHackEndpoint.ReadPort(Path.Combine(dir, "nao-existe.json")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class Reply(NetworkStream stream)
    {
        public Task Text(string text)
        {
            var fragment = new ProtoWriter().String(1, text).ToArray();
            var notification = NestedMessage(1, fragment);
            return Send(-3, notification);
        }

        public Task Result(byte[] body) => Send(-1, body);

        public async Task Fail(int code)
        {
            var header = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(header, -2);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), code);
            await stream.WriteAsync(header);
        }

        private async Task Send(short id, byte[] body)
        {
            var header = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(header, id);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), body.Length);
            await stream.WriteAsync(header);
            await stream.WriteAsync(body);
        }

        // Mensagem com um campo "bytes" (mensagem aninhada) no número indicado.
        private static byte[] NestedMessage(int field, byte[] nested)
        {
            var ms = new MemoryStream();
            ms.WriteByte((byte)((field << 3) | 2));
            var len = nested.Length;
            while (len >= 0x80)
            {
                ms.WriteByte((byte)(len | 0x80));
                len >>= 7;
            }
            ms.WriteByte((byte)len);
            ms.Write(nested);
            return ms.ToArray();
        }
    }

    /// <summary>Servidor que faz o handshake e responde cada chamada com o handler.</summary>
    private sealed class FakeDfHackServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task _loop;

        public FakeDfHackServer(Func<short, byte[], Reply, Task> handler)
        {
            _listener.Start();
            _loop = Task.Run(async () =>
            {
                using var tcp = await _listener.AcceptTcpClientAsync();
                var stream = tcp.GetStream();
                var hello = new byte[12];
                await stream.ReadExactlyAsync(hello);
                Assert.Equal("DFHack?\n", Encoding.ASCII.GetString(hello, 0, 8));
                var answer = new byte[12];
                Encoding.ASCII.GetBytes("DFHack!\n").CopyTo(answer, 0);
                BinaryPrimitives.WriteInt32LittleEndian(answer.AsSpan(8), 1);
                await stream.WriteAsync(answer);

                var header = new byte[8];
                while (true)
                {
                    try
                    {
                        await stream.ReadExactlyAsync(header);
                    }
                    catch (EndOfStreamException)
                    {
                        return;
                    }
                    var id = BinaryPrimitives.ReadInt16LittleEndian(header);
                    if (id == -4) return; // quit
                    var body = new byte[BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4))];
                    await stream.ReadExactlyAsync(body);
                    await handler(id, body, new Reply(stream));
                }
            });
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _loop.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
