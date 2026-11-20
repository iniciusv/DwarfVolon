using System.Text.Json;

namespace DwarfVolon.Core.Embark;

/// <summary>Uma função da fortaleza (ex.: Minerador) e os atributos que ajudam na habilidade dela.</summary>
public sealed record EmbarkRole
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Id da habilidade no jogo, ex.: MINING.</summary>
    public required string Skill { get; init; }

    public int Priority { get; init; } = 5;

    /// <summary>Quantos anões nessa função.</summary>
    public int Count { get; init; } = 1;

    public IReadOnlyList<string> Attributes { get; init; } = [];
    public string Why { get; init; } = "";
}

/// <summary>Catálogo de funções (recurso <c>Embark/Resources/funcoes.json</c>).</summary>
public static class EmbarkRoles
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<IReadOnlyList<EmbarkRole>> BuiltIn = new(LoadBuiltIn);

    public static IReadOnlyList<EmbarkRole> Default => BuiltIn.Value;

    public static IReadOnlyList<EmbarkRole> Parse(string json)
    {
        var file = JsonSerializer.Deserialize<RoleFile>(json, Options) ?? throw new InvalidDataException("Arquivo de funções vazio.");
        return file.Roles.OrderBy(r => r.Priority).ToList();
    }

    private static IReadOnlyList<EmbarkRole> LoadBuiltIn()
    {
        const string name = "DwarfVolon.Core.Embark.Resources.funcoes.json";
        using var stream = typeof(EmbarkRoles).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Recurso não encontrado: {name}");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private sealed class RoleFile
    {
        public List<EmbarkRole> Roles { get; set; } = [];
    }
}
