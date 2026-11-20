using System.Text.Json;
using System.Text.Json.Serialization;

namespace DwarfVolon.Core.Embark;

/// <summary>Em que tela o jogo estava quando lemos.</summary>
public enum EmbarkScreenState
{
    /// <summary>Tela "Preparar com cuidado" (viewscreen_setupdwarfgamest).</summary>
    Embark,

    /// <summary>Qualquer outra tela.</summary>
    OtherScreen,
}

/// <summary>Uma habilidade escolhida para o anão no embarque (nível 1 = Novato ... 5 = Proficiente).</summary>
public sealed record EmbarkSkill(string Id, int Level);

/// <summary>Um anão da tela de embarque, como o script <c>embark.lua</c> devolve.</summary>
public sealed class EmbarkDwarf
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public string NameEnglish { get; init; } = "";

    /// <summary>0 = fêmea, 1 = macho, outro = desconhecido.</summary>
    public int? Sex { get; init; }

    public int? BirthYear { get; init; }
    public string Profession { get; init; } = "";

    /// <summary>Pontos de habilidade que o anão ainda pode receber.</summary>
    public int PicksLeft { get; init; }

    public IReadOnlyList<EmbarkSkill> Skills { get; init; } = [];
    public IReadOnlyDictionary<string, int> Physical { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> Mental { get; init; } = new Dictionary<string, int>();

    /// <summary>Facetas de personalidade (0–100, 50 = neutro).</summary>
    public IReadOnlyDictionary<string, int> Traits { get; init; } = new Dictionary<string, int>();

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Anão {Index + 1}" : Name;

    public int SkillLevel(string skillId) => Skills.FirstOrDefault(s => s.Id == skillId)?.Level ?? 0;

    /// <summary>Total de níveis já escolhidos em todas as habilidades.</summary>
    public int SpentLevels => Skills.Sum(s => s.Level);

    /// <summary>Valor do atributo (físico ou mental) pelo id do jogo, ex.: STRENGTH.</summary>
    public int? Attribute(string id) =>
        Physical.TryGetValue(id, out var p) ? p : Mental.TryGetValue(id, out var m) ? m : null;
}

/// <summary>Leitura completa da tela de embarque.</summary>
public sealed class EmbarkSnapshot
{
    public EmbarkScreenState State { get; init; }

    /// <summary>Tipo da tela atual (quando não é a de embarque).</summary>
    public string Screen { get; init; } = "";

    /// <summary>Foco da tela, ex.: "setupdwarfgame/Dwarves".</summary>
    public string Focus { get; init; } = "";

    public int PointsRemaining { get; init; }
    public int EmbarkPoints { get; init; }
    public int CurrentYear { get; init; }
    public IReadOnlyList<EmbarkDwarf> Dwarves { get; init; } = [];

    /// <summary>Habilidades por aba da tela (CRUCIAL, LABOR, COMBAT, OTHER).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SkillTabs { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Faixa de cada atributo na casta (7 pontos; o 4º é a mediana).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<int>> AttributeRanges { get; init; } = new Dictionary<string, IReadOnlyList<int>>();

    /// <summary>Nome em inglês de cada habilidade, vindo do jogo.</summary>
    public IReadOnlyDictionary<string, string> SkillCaptions { get; init; } = new Dictionary<string, string>();

    public int? AgeOf(EmbarkDwarf dwarf) =>
        dwarf.BirthYear is { } born && CurrentYear > 0 && born > 0 ? CurrentYear - born : null;
}

public sealed class EmbarkReadException(string message) : Exception(message);

/// <summary>Converte o JSON do script <c>embark.lua</c> em <see cref="EmbarkSnapshot"/>.</summary>
public static class EmbarkJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new LenientDictionaryConverterFactory() },
    };

    /// <summary>
    /// Lê a saída do script. Aceita texto extra antes do JSON (avisos do console):
    /// usa a última linha que começa com <c>{</c>.
    /// </summary>
    public static EmbarkSnapshot Parse(string output)
    {
        var line = output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith('{'))
                   ?? throw new EmbarkReadException(
                       "O DFHack não devolveu dados. Saída: " + Truncate(output.Trim(), 300));

        Dto dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(line, Options) ?? throw new EmbarkReadException("Resposta vazia do DFHack.");
        }
        catch (JsonException ex)
        {
            throw new EmbarkReadException($"Resposta inválida do DFHack: {ex.Message}");
        }

        if (!dto.Ok) throw new EmbarkReadException($"Erro no script do DFHack: {dto.Error}");

        if (dto.Status != "embark")
            return new EmbarkSnapshot { State = EmbarkScreenState.OtherScreen, Screen = dto.Screen ?? "" };

        var ranges = new Dictionary<string, IReadOnlyList<int>>();
        foreach (var (key, value) in dto.Ranges?.Physical ?? []) ranges[key] = value;
        foreach (var (key, value) in dto.Ranges?.Mental ?? []) ranges[key] = value;

        return new EmbarkSnapshot
        {
            State = EmbarkScreenState.Embark,
            Focus = dto.Focus ?? "",
            PointsRemaining = dto.PointsRemaining,
            EmbarkPoints = dto.EmbarkPoints,
            CurrentYear = dto.CurYear,
            SkillTabs = (dto.SkillTabs ?? []).ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
            AttributeRanges = ranges,
            SkillCaptions = dto.SkillCaptions ?? [],
            Dwarves = (dto.Dwarves ?? []).Select(d => new EmbarkDwarf
            {
                Index = d.Index,
                Name = d.Name ?? "",
                NameEnglish = d.NameEnglish ?? "",
                Sex = d.Sex,
                BirthYear = d.BirthYear,
                Profession = d.Profession ?? "",
                PicksLeft = d.PicksLeft,
                Skills = (d.Skills ?? []).Where(s => !string.IsNullOrEmpty(s.Id)).Select(s => new EmbarkSkill(s.Id!, s.Level)).ToList(),
                Physical = d.Physical ?? [],
                Mental = d.Mental ?? [],
                Traits = d.Traits ?? [],
            }).ToList(),
        };
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";

    private sealed class Dto
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string? Status { get; set; }
        public string? Screen { get; set; }
        public string? Focus { get; set; }
        public int PointsRemaining { get; set; }
        public int EmbarkPoints { get; set; }
        public int CurYear { get; set; }
        public Dictionary<string, List<string>>? SkillTabs { get; set; }
        public Dictionary<string, string>? SkillCaptions { get; set; }
        public RangesDto? Ranges { get; set; }
        public List<DwarfDto>? Dwarves { get; set; }
    }

    private sealed class RangesDto
    {
        public Dictionary<string, int[]>? Physical { get; set; }
        public Dictionary<string, int[]>? Mental { get; set; }
    }

    private sealed class DwarfDto
    {
        public int Index { get; set; }
        public string? Name { get; set; }
        public string? NameEnglish { get; set; }
        public int? Sex { get; set; }
        public int? BirthYear { get; set; }
        public string? Profession { get; set; }
        public int PicksLeft { get; set; }
        public List<SkillDto>? Skills { get; set; }
        public Dictionary<string, int>? Physical { get; set; }
        public Dictionary<string, int>? Mental { get; set; }
        public Dictionary<string, int>? Traits { get; set; }
    }

    private sealed class SkillDto
    {
        public string? Id { get; set; }
        public int Level { get; set; }
    }

    /// <summary>
    /// O encoder de JSON do Lua escreve tabelas vazias como <c>[]</c>; para dicionários isso vira objeto vazio.
    /// </summary>
    private sealed class LenientDictionaryConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
            typeToConvert.GetGenericArguments()[0] == typeof(string);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(
                typeof(LenientDictionaryConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[1]))!;
    }

    private sealed class LenientDictionaryConverter<TValue> : JsonConverter<Dictionary<string, TValue>>
    {
        public override Dictionary<string, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                reader.Skip();
                return [];
            }
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Esperava um objeto.");

            var result = new Dictionary<string, TValue>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var key = reader.GetString()!;
                reader.Read();
                result[key] = JsonSerializer.Deserialize<TValue>(ref reader, options)!;
            }
            return result;
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, TValue> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, options);
    }
}
