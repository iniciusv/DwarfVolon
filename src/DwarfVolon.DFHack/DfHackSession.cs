using System.Text.Json;
using DwarfVolon.Core.DwarfFortress;

namespace DwarfVolon.DFHack;

/// <summary>Endereço do servidor RPC do DFHack.</summary>
public sealed record DfHackEndpoint(string Host, int Port)
{
    public static DfHackEndpoint Default { get; } = new(DfHackClient.DefaultHost, DfHackClient.DefaultPort);

    /// <summary>
    /// Mesma regra do DFHack: a variável <c>DFHACK_PORT</c> tem prioridade; senão,
    /// <c>dfhack-config/remote-server.json</c> da instalação; senão, 5000.
    /// </summary>
    public static DfHackEndpoint Resolve(DwarfFortressInstallation? installation)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("DFHACK_PORT"), out var envPort) && envPort > 0)
            return Default with { Port = envPort };

        if (installation is not null)
        {
            var config = Path.Combine(installation.InstallDirectory, "dfhack-config", "remote-server.json");
            if (ReadPort(config) is { } port) return Default with { Port = port };
        }
        return Default;
    }

    internal static int? ReadPort(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("port", out var p) && p.TryGetInt32(out var port) && port > 0 ? port : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public override string ToString() => $"{Host}:{Port}";
}

/// <summary>
/// Mantém uma conexão com o DFHack e reconecta quando ela cai (ex.: o jogo foi fechado e aberto de novo).
/// </summary>
public sealed class DfHackSession(Func<DfHackEndpoint> endpoint) : IAsyncDisposable
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private DfHackClient? _client;

    /// <summary>Versões lidas na última conexão (ex.: "DFHack 53.17-r1 · DF v0.53.17").</summary>
    public string? VersionText { get; private set; }

    public bool IsConnected => _client?.IsConnected == true;

    public async Task<DfHackClient> GetClientAsync(CancellationToken ct = default)
    {
        if (_client is { IsConnected: true } current) return current;

        await _connectLock.WaitAsync(ct);
        try
        {
            if (_client is { IsConnected: true } again) return again;
            if (_client is not null) await _client.DisposeAsync();
            _client = null;

            var ep = endpoint();
            var client = await DfHackClient.ConnectAsync(ep.Host, ep.Port, ct: ct);
            try
            {
                VersionText = $"DFHack {await client.GetDfHackVersionAsync(ct)} · DF {await client.GetDfVersionAsync(ct)}";
            }
            catch (DfHackException)
            {
                VersionText = "DFHack (versão desconhecida)";
            }
            _client = client;
            return client;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>Roda uma ação com o cliente; se a conexão tiver caído, reconecta e tenta mais uma vez.</summary>
    public async Task<T> RunAsync<T>(Func<DfHackClient, Task<T>> action, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        try
        {
            return await action(client);
        }
        catch (DfHackException) when (!client.IsConnected)
        {
            return await action(await GetClientAsync(ct));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null) await _client.DisposeAsync();
        _client = null;
    }
}
