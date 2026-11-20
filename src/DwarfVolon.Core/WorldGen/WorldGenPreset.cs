using System.Globalization;
using System.Text.Json;

namespace DwarfVolon.Core.WorldGen;

/// <summary>
/// Um "patch" aplicado sobre um conjunto base. É o motor por trás das opções de modificador
/// (<see cref="ModifierOption"/>): como a maioria das operações multiplica o valor da base,
/// a mesma opção funciona para mundos pequenos e grandes.
/// </summary>
public sealed class WorldGenPreset
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public List<PresetOperation> Operations { get; init; } = [];
}

/// <summary>
/// Operação sobre um token. Cada lista é posicional sobre os valores do token (sem a chave);
/// <c>null</c> numa posição significa "não mexer". A ordem de aplicação é:
/// set → multiply → add → min → max.
/// </summary>
public sealed class PresetOperation
{
    public required string Token { get; init; }

    /// <summary>Para tokens com chave, como REGION_COUNTS (ex.: "MOUNTAINS").</summary>
    public string? Key { get; init; }

    /// <summary>Valores fixos (número ou texto).</summary>
    public List<JsonElement>? Set { get; init; }

    public List<double?>? Multiply { get; init; }

    public List<double?>? Add { get; init; }

    /// <summary>Garante pelo menos este valor.</summary>
    public List<double?>? Min { get; init; }

    /// <summary>Limita a no máximo este valor.</summary>
    public List<double?>? Max { get; init; }
}

public sealed record PresetChange(string PresetName, string TokenText, string? Before, string After);

public sealed class PresetApplyResult
{
    public List<PresetChange> Changes { get; } = [];
    public List<string> Warnings { get; } = [];
}

public static class PresetApplier
{
    public static PresetApplyResult Apply(WorldGenParameterSet target, IEnumerable<WorldGenPreset> presets)
    {
        var result = new PresetApplyResult();
        foreach (var preset in presets) Apply(target, preset, result);
        return result;
    }

    public static PresetApplyResult Apply(WorldGenParameterSet target, WorldGenPreset preset) => Apply(target, [preset]);

    private static void Apply(WorldGenParameterSet target, WorldGenPreset preset, PresetApplyResult result)
    {
        foreach (var op in preset.Operations)
        {
            var label = op.Key is null ? op.Token : $"{op.Token}:{op.Key}";
            var existing = target.Find(op.Token, op.Key);
            List<string?> values;

            if (existing is not null)
            {
                values = existing.Values.Select(v => (string?)v).ToList();
            }
            else if (op.Set is { Count: > 0 } && op.Set.All(e => e.ValueKind != JsonValueKind.Null)
                     && op.Multiply is null && op.Add is null)
            {
                values = [];
            }
            else
            {
                result.Warnings.Add($"[{preset.Name}] {label} não existe na base; operação ignorada.");
                continue;
            }

            var length = new[] { values.Count, op.Set?.Count ?? 0 }.Max();
            while (values.Count < length) values.Add(null);

            for (var i = 0; i < values.Count; i++)
            {
                if (op.Set is not null && i < op.Set.Count && op.Set[i].ValueKind != JsonValueKind.Null)
                    values[i] = op.Set[i].ValueKind == JsonValueKind.String ? op.Set[i].GetString() : op.Set[i].GetRawText();

                values[i] = Numeric(values[i], At(op.Multiply, i), (v, f) => v * f, label, preset, result);
                values[i] = Numeric(values[i], At(op.Add, i), (v, a) => v + a, label, preset, result);
                values[i] = Numeric(values[i], At(op.Min, i), Math.Max, label, preset, result);
                values[i] = Numeric(values[i], At(op.Max, i), Math.Min, label, preset, result);
            }

            if (values.Any(v => v is null))
            {
                result.Warnings.Add($"[{preset.Name}] {label} ficou com valores vazios; operação ignorada.");
                continue;
            }

            var finalValues = values.Select(v => v!).ToList();
            var updated = op.Key is null
                ? new WorldGenToken(op.Token, finalValues)
                : new WorldGenToken(op.Token, finalValues.Prepend(op.Key));

            if (existing is not null && existing.Equals(updated)) continue;
            target.Set(updated);
            result.Changes.Add(new PresetChange(preset.Name, label, existing is null ? null : string.Join(':', existing.Values), string.Join(':', updated.Values)));
        }
    }

    private static double? At(List<double?>? list, int i) => list is not null && i < list.Count ? list[i] : null;

    private static string? Numeric(string? value, double? operand, Func<double, double, double> op,
        string label, WorldGenPreset preset, PresetApplyResult result)
    {
        if (operand is null || value is null) return value;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            result.Warnings.Add($"[{preset.Name}] {label}: valor '{value}' não é numérico.");
            return value;
        }

        var computed = Math.Max(0, Math.Round(op(number, operand.Value), MidpointRounding.AwayFromZero));
        return ((long)computed).ToString(CultureInfo.InvariantCulture);
    }
}
