using System.Text.Json;
using DwarfVolon.Core.Embark;

namespace DwarfVolon.DFHack;

/// <summary>
/// Aplica planos de pontos na tela "Preparar com cuidado" (escrita no jogo). O script valida tudo antes
/// de mudar: ou aplica todos os anões pedidos, ou nenhum.
/// </summary>
public sealed class EmbarkWriter(DfHackSession session)
{
    private static readonly Lazy<string> Script = new(() => DfHackScripts.Load("embark_apply.lua"));

    public async Task<EmbarkApplyResult> ApplyAsync(IReadOnlyList<EmbarkApplyItem> items, CancellationToken ct = default)
    {
        var code = BuildCode(items);
        var result = await session.RunAsync(c => c.RunLuaAsync(code, ct), ct);
        if (!result.Success)
            throw new EmbarkReadException($"O DFHack recusou o comando ({result.Code}). {result.Output.Trim()}");
        return EmbarkApplyResult.Parse(result.Output);
    }

    /// <summary>Código Lua enviado: o plano em JSON numa string longa + o script.</summary>
    internal static string BuildCode(IReadOnlyList<EmbarkApplyItem> items)
    {
        var plan = JsonSerializer.Serialize(new
        {
            // null em JSON vira um valor especial no Lua; sem unidade, o campo simplesmente não vai.
            dwarves = items.Select(i =>
            {
                var d = new Dictionary<string, object> { ["index"] = i.Index, ["skills"] = i.Skills };
                if (i.UnitId is { } unit) d["unit_id"] = unit;
                return d;
            }),
        });
        if (plan.Contains("]==]")) throw new ArgumentException("Plano com texto inválido.", nameof(items));
        return $"local PLAN_JSON = [==[{plan}]==]\n{Script.Value}";
    }
}
