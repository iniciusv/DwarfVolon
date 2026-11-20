using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DwarfVolon.Core.WorldGen;

/// <summary>
/// Uma categoria de modificador (ex.: "Rios") com níveis mutuamente exclusivos
/// (ex.: "Poucos", "Padrão", "Mais", "Muitos"). O usuário escolhe um nível por categoria.
/// </summary>
public sealed class ModifierCategory
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Icon { get; init; } = "";
    public string Description { get; init; } = "";
    public int Order { get; init; }
    public List<ModifierOption> Options { get; init; } = [];

    /// <summary>Origem (embutido ou caminho do arquivo). Preenchido ao carregar.</summary>
    [JsonIgnore]
    public string Source { get; set; } = "";

    [JsonIgnore]
    public ModifierOption DefaultOption => Options.FirstOrDefault(o => o.IsDefault) ?? Options[0];

    public ModifierOption? FindOption(string id) =>
        Options.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
}

public sealed class ModifierOption
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";

    /// <summary>A opção "Padrão": não altera nada da base.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Palavra curta usada para sugerir o título do conjunto (ex.: "MAIS RIOS").</summary>
    public string? TitleTag { get; init; }

    /// <summary>Avisos (ex.: risco de rejeição de mapas).</summary>
    public List<string> Notes { get; init; } = [];

    public List<PresetOperation> Operations { get; init; } = [];

    public WorldGenPreset ToPreset(ModifierCategory category) => new()
    {
        Id = $"{category.Id}/{Id}",
        Name = $"{category.Name}: {Name}",
        Description = Description,
        Operations = Operations,
    };
}

/// <summary>Combinação pronta de níveis (ex.: "Rios e vulcões" = Rios: mais + Vulcanismo: mais).</summary>
public sealed class WorldGenProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";

    /// <summary>Id da categoria → id da opção. Categorias ausentes voltam ao padrão.</summary>
    public Dictionary<string, string> Selections { get; init; } = [];
}

public static class WorldGenJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };
}

/// <summary>
/// Carrega categorias de modificadores e perfis. Embutidos ficam em WorldGen/Modifiers;
/// o usuário pode adicionar categorias (ou substituir uma embutida usando o mesmo id)
/// colocando arquivos .json numa pasta.
/// </summary>
public static class ModifierLibrary
{
    private const string ResourcePrefix = "DwarfVolon.Core.WorldGen.Modifiers.";
    private const string ProfilesResource = ResourcePrefix + "_perfis.json";

    public static IReadOnlyList<ModifierCategory> LoadBuiltInCategories()
    {
        var assembly = typeof(ModifierLibrary).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal) && n != ProfilesResource)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                var category = Deserialize<ModifierCategory>(stream, name);
                category.Source = "embutido";
                return category;
            })
            .OrderBy(c => c.Order)
            .ToList();
    }

    public static IReadOnlyList<WorldGenProfile> LoadBuiltInProfiles()
    {
        using var stream = typeof(ModifierLibrary).Assembly.GetManifestResourceStream(ProfilesResource)
            ?? throw new InvalidOperationException($"Recurso não encontrado: {ProfilesResource}");
        return Deserialize<List<WorldGenProfile>>(stream, ProfilesResource);
    }

    /// <summary>Carrega categorias de uma pasta. Arquivos inválidos viram mensagens em <paramref name="errors"/>.</summary>
    public static IReadOnlyList<ModifierCategory> LoadCategoriesFromDirectory(string directory, ICollection<string>? errors = null)
    {
        if (!Directory.Exists(directory)) return [];
        var categories = new List<ModifierCategory>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var category = Deserialize<ModifierCategory>(stream, file);
                if (category.Options.Count == 0) throw new InvalidDataException("a categoria não tem opções");
                category.Source = file;
                categories.Add(category);
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
            {
                errors?.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return categories;
    }

    /// <summary>Embutidas + do usuário; uma categoria do usuário com o mesmo id substitui a embutida.</summary>
    public static IReadOnlyList<ModifierCategory> Merge(IEnumerable<ModifierCategory> builtIn, IEnumerable<ModifierCategory> user)
    {
        var byId = new Dictionary<string, ModifierCategory>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in builtIn.Concat(user)) byId[category.Id] = category;
        return byId.Values.OrderBy(c => c.Order).ThenBy(c => c.Name, StringComparer.CurrentCulture).ToList();
    }

    private static T Deserialize<T>(Stream stream, string origin) =>
        JsonSerializer.Deserialize<T>(stream, WorldGenJson.Options) ?? throw new InvalidDataException($"Arquivo vazio: {origin}");
}

/// <summary>Conjuntos padrão do jogo embutidos no app, usados como base quando o usuário não tem um world_gen.txt.</summary>
public static class BuiltInTemplates
{
    public const string DefaultTitle = "MEDIUM REGION";
    private const string ResourceName = "DwarfVolon.Core.WorldGen.Resources.df047_default_world_gen.txt";

    public static WorldGenDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso não encontrado: {ResourceName}");
        using var reader = new StreamReader(stream, WorldGenDocument.FileEncoding);
        return WorldGenDocument.Parse(reader.ReadToEnd());
    }
}
