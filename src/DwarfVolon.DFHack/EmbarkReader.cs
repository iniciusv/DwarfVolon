using DwarfVolon.Core.Embark;

namespace DwarfVolon.DFHack;

/// <summary>Scripts Lua embutidos no assembly (pasta <c>Scripts</c>).</summary>
public static class DfHackScripts
{
    public static string Load(string fileName)
    {
        var name = $"DwarfVolon.DFHack.Scripts.{fileName}";
        using var stream = typeof(DfHackScripts).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Script não encontrado: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// Lê a tela "Preparar com cuidado" do jogo aberto. O script vai inteiro pelo comando <c>lua</c>
/// do DFHack, então nada é instalado na pasta do jogo.
/// </summary>
public sealed class EmbarkReader(DfHackSession session)
{
    private static readonly Lazy<string> Script = new(() => DfHackScripts.Load("embark.lua"));

    public async Task<EmbarkSnapshot> ReadAsync(CancellationToken ct = default)
    {
        var result = await session.RunAsync(c => c.RunLuaAsync(Script.Value, ct), ct);
        if (!result.Success)
            throw new EmbarkReadException($"O DFHack recusou o comando ({result.Code}). {result.Output.Trim()}");
        return EmbarkJson.Parse(result.Output);
    }
}
