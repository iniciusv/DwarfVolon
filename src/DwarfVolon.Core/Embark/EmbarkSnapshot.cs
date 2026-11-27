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

/// <summary>Um gosto (ou ódio) do anão. <see cref="Kind"/> é o tipo do jogo (ex.: LikeMaterial); <see cref="Text"/> vem em inglês, como no jogo.</summary>
public sealed record EmbarkPreference(string Kind, string Text);

/// <summary>Uma crença pessoal (valor) do anão. <see cref="Strength"/> vai de -50 a 50 (0 = indiferente).</summary>
public sealed record EmbarkBelief(string Id, int Strength);

/// <summary>
/// Um item escolhido para levar. <see cref="Type"/> é o tipo do jogo (ex.: WEAPON), <see cref="Subtype"/> o id do
/// subtipo (ex.: ITEM_WEAPON_PICK, vazio se não tiver), <see cref="Material"/> o token (ex.: INORGANIC:IRON) e
/// <see cref="Name"/> o nome no singular, em inglês, como o jogo mostra. <see cref="Line"/> é a linha da lista do jogo.
/// </summary>
public sealed record EmbarkItem(int Line, string Type, string Subtype, string Material, string Name, int Stack);

/// <summary>Uma linha da lista de itens do jogo e quantas opções (materiais) dá para escolher nela.</summary>
public sealed record EmbarkItemLine(int Line, string Type, string Subtype, int Options);

/// <summary>
/// Uma opção de animal da tela de embarque (espécie + casta + tipo) e quantos foram escolhidos.
/// <see cref="Profession"/>: STANDARD (adulto), CHILD (filhote), TRAINED_HUNTER, TRAINED_WAR.
/// <see cref="Value"/> é o valor do animal no jogo (PETVALUE).
/// </summary>
public sealed record EmbarkPet(int Index, string Creature, string Name, string Caste, string Profession, int Value,
    bool Grazer, bool LaysEggs, bool Milkable, bool HuntsVermin, int Count)
{
    public bool IsFemale => Caste == "FEMALE";
    public bool IsMale => Caste == "MALE";
    public bool IsAdult => Profession != "CHILD";
}

/// <summary>
/// Ficha de unidade aberta no jogo no momento da leitura. O DF só gera o texto de descrição
/// (<c>view_sheets.personality_raw_str</c>) com a ficha aberta.
/// </summary>
public sealed record EmbarkOpenSheet(int UnitId, int SubTab, int PersonalityTab, IReadOnlyList<string> Blocks)
{
    /// <summary>Valor de <c>active_sub_tab</c> da aba Personalidade (conferido no DF 53.17).</summary>
    public const int PersonalitySubTab = 10;

    public bool IsPersonality => SubTab == PersonalitySubTab && Blocks.Any(b => !string.IsNullOrWhiteSpace(b));
}

/// <summary>Um anão da tela de embarque, como o script <c>embark.lua</c> devolve.</summary>
public sealed class EmbarkDwarf
{
    public int Index { get; init; }

    /// <summary>Id da unidade no jogo (liga o anão à ficha aberta).</summary>
    public int? UnitId { get; init; }

    /// <summary>Id da figura histórica: continua o mesmo depois do embarque.</summary>
    public int? HistFigureId { get; init; }

    public string Name { get; init; } = "";
    public string NameEnglish { get; init; } = "";
    public string Nickname { get; init; } = "";

    /// <summary>0 = fêmea, 1 = macho, outro = desconhecido.</summary>
    public int? Sex { get; init; }

    public int? BirthYear { get; init; }

    /// <summary>Idade em anos, calculada pelo jogo.</summary>
    public int? Age { get; init; }
    public string Profession { get; init; } = "";

    /// <summary>Pontos de habilidade que o anão ainda pode receber.</summary>
    public int PicksLeft { get; init; }

    /// <summary>Habilidades escolhidas na tela de embarque (gastam os pontos de habilidade).</summary>
    public IReadOnlyList<EmbarkSkill> Skills { get; init; } = [];

    /// <summary>Habilidades que o anão já traz (ex.: música, poesia); não gastam pontos.</summary>
    public IReadOnlyList<EmbarkSkill> BackgroundSkills { get; init; } = [];
    public IReadOnlyDictionary<string, int> Physical { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> Mental { get; init; } = new Dictionary<string, int>();

    /// <summary>Facetas de personalidade (0–100, 50 = neutro).</summary>
    public IReadOnlyDictionary<string, int> Traits { get; init; } = new Dictionary<string, int>();

    /// <summary>Gostos e ódios, na ordem do jogo.</summary>
    public IReadOnlyList<EmbarkPreference> Preferences { get; init; } = [];

    /// <summary>Crenças que diferem da cultura do anão.</summary>
    public IReadOnlyList<EmbarkBelief> Beliefs { get; init; } = [];

    /// <summary>Sonhos (ids do jogo, ex.: CREATE_A_GREAT_WORK_OF_ART).</summary>
    public IReadOnlyList<string> Dreams { get; init; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Anão {Index + 1}" : Name;

    /// <summary>Nível na habilidade: o maior entre o escolhido no embarque e o que o anão já traz.</summary>
    public int SkillLevel(string skillId) =>
        Math.Max(Skills.FirstOrDefault(s => s.Id == skillId)?.Level ?? 0,
                 BackgroundSkills.FirstOrDefault(s => s.Id == skillId)?.Level ?? 0);

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

    /// <summary>Ficha aberta no jogo, se for de um dos anões do embarque.</summary>
    public EmbarkOpenSheet? Sheet { get; init; }

    public int PointsRemaining { get; init; }
    public int EmbarkPoints { get; init; }
    public int CurrentYear { get; init; }

    /// <summary>Pasta do mundo no save (ex.: region2). Chave para guardar as decisões do embarque.</summary>
    public string SaveDir { get; init; } = "";

    /// <summary>Nome do mundo (em inglês).</summary>
    public string World { get; init; } = "";
    public IReadOnlyList<EmbarkDwarf> Dwarves { get; init; } = [];

    /// <summary>Habilidades por aba da tela (CRUCIAL, LABOR, COMBAT, OTHER).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SkillTabs { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Faixa de cada atributo na casta (7 pontos; o 4º é a mediana).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<int>> AttributeRanges { get; init; } = new Dictionary<string, IReadOnlyList<int>>();

    /// <summary>Nome em inglês de cada habilidade, vindo do jogo.</summary>
    public IReadOnlyDictionary<string, string> SkillCaptions { get; init; } = new Dictionary<string, string>();

    /// <summary>Itens escolhidos para levar.</summary>
    public IReadOnlyList<EmbarkItem> Items { get; init; } = [];

    /// <summary>Linhas da lista de itens do jogo (o que dá para comprar).</summary>
    public IReadOnlyList<EmbarkItemLine> ItemLines { get; init; } = [];

    /// <summary>Opções de animais (todas, com a quantidade escolhida em cada).</summary>
    public IReadOnlyList<EmbarkPet> Pets { get; init; } = [];

    public int? AgeOf(EmbarkDwarf dwarf) =>
        dwarf.Age ?? (dwarf.BirthYear is { } born && CurrentYear > 0 && born > 0 ? CurrentYear - born : null);
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
            Sheet = dto.Sheet is { } sh ? new EmbarkOpenSheet(sh.UnitId, sh.SubTab, sh.PersonalityTab, sh.Blocks ?? []) : null,
            PointsRemaining = dto.PointsRemaining,
            EmbarkPoints = dto.EmbarkPoints,
            CurrentYear = dto.CurYear,
            SaveDir = dto.SaveDir ?? "",
            World = dto.World ?? "",
            SkillTabs = (dto.SkillTabs ?? []).ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
            AttributeRanges = ranges,
            SkillCaptions = dto.SkillCaptions ?? [],
            Items = (dto.Items ?? []).Select(i => new EmbarkItem(i.Line, i.Type ?? "", i.Subtype ?? "", i.Material ?? "", i.Name ?? "", i.Stack)).ToList(),
            ItemLines = (dto.ItemLines ?? []).Select(l => new EmbarkItemLine(l.Line, l.Type ?? "", l.Subtype ?? "", l.Options)).ToList(),
            Pets = (dto.Pets ?? []).Where(p => !string.IsNullOrEmpty(p.Creature)).Select(p => new EmbarkPet(p.Index, p.Creature!,
                p.Name ?? p.Creature!, p.Caste ?? "", p.Profession ?? "", p.Value, p.Grazer, p.LaysEggs, p.Milkable, p.HuntsVermin, p.Count)).ToList(),
            Dwarves = (dto.Dwarves ?? []).Select(d => new EmbarkDwarf
            {
                Index = d.Index,
                UnitId = d.UnitId,
                HistFigureId = d.HistFigureId,
                Name = d.Name ?? "",
                NameEnglish = d.NameEnglish ?? "",
                Nickname = d.Nickname ?? "",
                Age = d.Age,
                Sex = d.Sex,
                BirthYear = d.BirthYear,
                Profession = d.Profession ?? "",
                PicksLeft = d.PicksLeft,
                Skills = ToSkills(d.Skills),
                BackgroundSkills = ToSkills(d.BackgroundSkills),
                Preferences = (d.Preferences ?? []).Select(p => new EmbarkPreference(p.Kind ?? "", p.Text ?? "")).ToList(),
                Beliefs = (d.Beliefs ?? []).Where(b => !string.IsNullOrEmpty(b.Id)).Select(b => new EmbarkBelief(b.Id!, b.Strength)).ToList(),
                Dreams = d.Dreams ?? [],
                Physical = d.Physical ?? [],
                Mental = d.Mental ?? [],
                Traits = d.Traits ?? [],
            }).ToList(),
        };
    }

    private static List<EmbarkSkill> ToSkills(List<SkillDto>? skills) =>
        (skills ?? []).Where(s => !string.IsNullOrEmpty(s.Id)).Select(s => new EmbarkSkill(s.Id!, s.Level)).ToList();

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
        public string? SaveDir { get; set; }
        public string? World { get; set; }
        public Dictionary<string, List<string>>? SkillTabs { get; set; }
        public Dictionary<string, string>? SkillCaptions { get; set; }
        public RangesDto? Ranges { get; set; }
        public List<DwarfDto>? Dwarves { get; set; }
        public SheetDto? Sheet { get; set; }
        public List<ItemDto>? Items { get; set; }
        public List<ItemLineDto>? ItemLines { get; set; }
        public List<PetDto>? Pets { get; set; }
    }

    private sealed class ItemDto
    {
        public int Line { get; set; }
        public string? Type { get; set; }
        public string? Subtype { get; set; }
        public string? Material { get; set; }
        public string? Name { get; set; }
        public int Stack { get; set; }
    }

    private sealed class ItemLineDto
    {
        public int Line { get; set; }
        public string? Type { get; set; }
        public string? Subtype { get; set; }
        public int Options { get; set; }
    }

    private sealed class PetDto
    {
        public int Index { get; set; }
        public string? Creature { get; set; }
        public string? Name { get; set; }
        public string? Caste { get; set; }
        public string? Profession { get; set; }
        public int Value { get; set; }
        public bool Grazer { get; set; }
        public bool LaysEggs { get; set; }
        public bool Milkable { get; set; }
        public bool HuntsVermin { get; set; }
        public int Count { get; set; }
    }

    private sealed class SheetDto
    {
        public int UnitId { get; set; }
        public int SubTab { get; set; }
        public int PersonalityTab { get; set; }
        public List<string>? Blocks { get; set; }
    }

    private sealed class PreferenceDto
    {
        public string? Kind { get; set; }
        public string? Text { get; set; }
    }

    private sealed class BeliefDto
    {
        public string? Id { get; set; }
        public int Strength { get; set; }
    }

    private sealed class RangesDto
    {
        public Dictionary<string, int[]>? Physical { get; set; }
        public Dictionary<string, int[]>? Mental { get; set; }
    }

    private sealed class DwarfDto
    {
        public int Index { get; set; }
        public int? UnitId { get; set; }
        public int? HistFigureId { get; set; }
        public string? Name { get; set; }
        public string? NameEnglish { get; set; }
        public string? Nickname { get; set; }
        public int? Age { get; set; }
        public int? Sex { get; set; }
        public int? BirthYear { get; set; }
        public string? Profession { get; set; }
        public int PicksLeft { get; set; }
        public List<SkillDto>? Skills { get; set; }
        public List<SkillDto>? BackgroundSkills { get; set; }
        public List<PreferenceDto>? Preferences { get; set; }
        public List<BeliefDto>? Beliefs { get; set; }
        public List<string>? Dreams { get; set; }
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
