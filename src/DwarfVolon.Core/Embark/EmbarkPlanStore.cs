using System.Text.Json;
using DwarfVolon.Core.Settings;

namespace DwarfVolon.Core.Embark;

/// <summary>
/// Decisões do embarque salvas em disco (uma por mundo), para sobreviver ao fechamento do app e,
/// depois do embarque, servir para designar os trabalhos (ex.: só os mineradores mineram).
/// Os anões são reconhecidos pela figura histórica, depois pela unidade e, por fim, pelo nome.
/// </summary>
public static class EmbarkPlanStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DirectoryPath => Path.Combine(AppPaths.AppDataDirectory, "embarques");

    /// <summary>Arquivo do mundo (pela pasta do save, ex.: region2). Nulo se o jogo não informou a pasta.</summary>
    public static string? PathFor(EmbarkSnapshot snapshot, string? directory = null) =>
        string.IsNullOrWhiteSpace(snapshot.SaveDir)
            ? null
            : Path.Combine(directory ?? DirectoryPath, $"{string.Concat(snapshot.SaveDir.Split(Path.GetInvalidFileNameChars()))}.json");

    public static void Save(string path, EmbarkSnapshot snapshot, EmbarkPlan plan)
    {
        var file = new EmbarkPlanFile
        {
            World = snapshot.World,
            SaveDir = snapshot.SaveDir,
            UpdatedAt = DateTime.Now,
            Dwarves = snapshot.Dwarves.Where(plan.Has).Select(d =>
            {
                var origin = plan.OriginOf(d);
                return new EmbarkPlanFile.Entry
                {
                    HistFigureId = d.HistFigureId,
                    UnitId = d.UnitId,
                    Name = d.DisplayName,
                    Roles = [.. plan.RolesOf(d)],
                    Skills = plan.For(d).ToDictionary(kv => kv.Key, kv => kv.Value),
                    Origin = origin?.Source ?? PlanSource.Manual,
                    OriginAt = origin?.At,
                    EditedByHand = origin?.EditedByHand ?? false,
                };
            }).ToList(),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(file, Options));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Carrega as decisões salvas para os anões da tela. Devolve quantos anões foram restaurados.</summary>
    public static int Load(string path, EmbarkSnapshot snapshot, EmbarkPlan plan)
    {
        var file = Read(path);
        if (file is null) return 0;

        var restored = 0;
        foreach (var entry in file.Dwarves)
        {
            var dwarf = Match(snapshot, entry);
            if (dwarf is null) continue;
            plan.Restore(dwarf, entry.Skills, entry.Roles,
                new PlanOrigin(entry.Origin, entry.OriginAt ?? file.UpdatedAt, entry.EditedByHand));
            restored++;
        }
        return restored;
    }

    public static EmbarkPlanFile? Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<EmbarkPlanFile>(File.ReadAllText(path), Options) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static EmbarkDwarf? Match(EmbarkSnapshot snapshot, EmbarkPlanFile.Entry entry) =>
        snapshot.Dwarves.FirstOrDefault(d => entry.HistFigureId is { } hf && d.HistFigureId == hf)
        ?? snapshot.Dwarves.FirstOrDefault(d => entry.UnitId is { } u && d.UnitId == u)
        ?? snapshot.Dwarves.FirstOrDefault(d => EmbarkAiProtocol.Normalize(d.DisplayName) == EmbarkAiProtocol.Normalize(entry.Name));
}

/// <summary>Formato do arquivo de decisões do embarque.</summary>
public sealed class EmbarkPlanFile
{
    public string World { get; set; } = "";
    public string SaveDir { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
    public List<Entry> Dwarves { get; set; } = [];

    public sealed class Entry
    {
        public int? HistFigureId { get; set; }
        public int? UnitId { get; set; }
        public string Name { get; set; } = "";

        /// <summary>Ids das funções (catálogo funcoes.json), ex.: "minerador".</summary>
        public List<string> Roles { get; set; } = [];

        /// <summary>Plano de pontos: habilidade → nível.</summary>
        public Dictionary<string, int> Skills { get; set; } = [];

        public PlanSource Origin { get; set; }
        public DateTime? OriginAt { get; set; }
        public bool EditedByHand { get; set; }
    }
}

/// <summary>Plano de um anão para aplicar no jogo.</summary>
public sealed record EmbarkApplyItem(int Index, int? UnitId, IReadOnlyDictionary<string, int> Skills)
{
    public static EmbarkApplyItem From(EmbarkDwarf dwarf, EmbarkPlan plan) =>
        new(dwarf.Index, dwarf.UnitId, plan.For(dwarf).ToDictionary(kv => kv.Key, kv => kv.Value));
}

/// <summary>Resposta do script de aplicação.</summary>
public sealed class EmbarkApplyResult
{
    public bool Ok { get; init; }
    public string Error { get; init; } = "";
    public int AppliedCount { get; init; }

    public static EmbarkApplyResult Parse(string output)
    {
        var line = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith('{'))
                   ?? throw new EmbarkReadException("O DFHack não respondeu à aplicação. Saída: " + output.Trim());
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        var ok = root.TryGetProperty("ok", out var o) && o.ValueKind == JsonValueKind.True;
        return new EmbarkApplyResult
        {
            Ok = ok,
            Error = root.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "",
            AppliedCount = root.TryGetProperty("applied", out var a) && a.ValueKind == JsonValueKind.Array ? a.GetArrayLength() : 0,
        };
    }
}
