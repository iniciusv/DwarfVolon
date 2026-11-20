namespace DwarfVolon.Core.WorldGen;

public enum WorldGenArgKind
{
    Number,
    Flag,
    Choice,
    Text,
}

public sealed record WorldGenArgInfo(string Label, WorldGenArgKind Kind = WorldGenArgKind.Number, IReadOnlyList<string>? Choices = null);

public sealed record WorldGenTokenInfo(
    string Name,
    string Category,
    string DisplayName,
    string Description,
    IReadOnlyList<WorldGenArgInfo> Args,
    bool IsKeyed = false,
    bool IsEditable = true);

/// <summary>
/// Metadados dos tokens do world_gen.txt (DF 53.x). A ordem da lista é a mesma em que o jogo
/// (e o script export-worldgen do DFHack) grava os tokens, e serve para inserir tokens novos
/// no lugar certo.
/// </summary>
public static class WorldGenTokenCatalog
{
    public const string General = "Geral";
    public const string Terrain = "Terreno e clima";
    public const string Rivers = "Rios e erosão";
    public const string Volcanoes = "Vulcões e minerais";
    public const string Creatures = "Criaturas e ameaças";
    public const string Magic = "Magia e sobrenatural";
    public const string Rejection = "Mínimos para aceitar o mapa";
    public const string Biomes = "Biomas (mínimos)";
    public const string Underground = "Subsolo e cavernas";
    public const string Civilizations = "Civilizações e história";
    public const string Seeds = "Seeds";
    public const string Painter = "Pintura do mapa (PS_*)";
    public const string Other = "Outros";

    public static IReadOnlyList<string> CategoryOrder { get; } =
    [
        General, Rivers, Volcanoes, Terrain, Biomes, Rejection, Creatures, Magic, Underground, Civilizations, Seeds, Other,
    ];

    private static readonly string[] PoleChoices = ["NONE", "NORTH_OR_SOUTH", "NORTH_AND_OR_SOUTH", "NORTH", "SOUTH", "NORTH_AND_SOUTH"];
    private static readonly string[] ExtinctChoices = ["EXTINCT", "ISOLATED", "UNTAMED_WILDS", "WILDERNESS", "DOMESTICATED"];
    private static readonly string[] BiomeChoices = ["SWAMP", "DESERT", "FOREST", "MOUNTAINS", "OCEAN", "GLACIER", "TUNDRA", "GRASSLAND", "HILLS"];

    private static WorldGenArgInfo N(string label) => new(label);
    private static WorldGenArgInfo F(string label = "Ativo") => new(label, WorldGenArgKind.Flag);
    private static WorldGenArgInfo T(string label) => new(label, WorldGenArgKind.Text);
    private static WorldGenArgInfo C(string label, string[] choices) => new(label, WorldGenArgKind.Choice, choices);

    private static readonly WorldGenArgInfo[] RangeArgs = [N("Mínimo"), N("Máximo"), N("Variação X"), N("Variação Y")];
    private static readonly WorldGenArgInfo[] FrequencyArgs =
        [N("Malha"), N("Peso 0–20%"), N("Peso 20–40%"), N("Peso 40–60%"), N("Peso 60–80%"), N("Peso 80–100%")];
    private static readonly WorldGenArgInfo[] LowMidHigh = [N("Baixo"), N("Médio"), N("Alto")];
    private static readonly WorldGenArgInfo[] SubregionSizes = [N("Sub-região pequena"), N("Média"), N("Grande")];
    private static readonly WorldGenArgInfo[] AttackTrigger = [N("População"), N("Riqueza exportada"), N("Riqueza criada")];
    private static readonly WorldGenArgInfo[] Single = [N("Valor")];

    private static WorldGenTokenInfo Tok(string name, string category, string display, string description, params WorldGenArgInfo[] args) =>
        new(name, category, display, description, args.Length == 0 ? Single : args);

    public static IReadOnlyList<WorldGenTokenInfo> All { get; } =
    [
        Tok("TITLE", General, "Título", "Nome do conjunto de parâmetros na tela de criação de mundo.", T("Título")),
        Tok("CUSTOM_NAME", General, "Nome do mundo", "Nome fixo para o mundo gerado (opcional).", T("Nome")),
        Tok("SEED", Seeds, "Seed do mundo", "Seed principal. Sem ela, cada geração é diferente.", T("Seed")),
        Tok("HISTORY_SEED", Seeds, "Seed da história", "Seed usada na simulação da história.", T("Seed")),
        Tok("NAME_SEED", Seeds, "Seed de nomes", "Seed usada para gerar nomes.", T("Seed")),
        Tok("CREATURE_SEED", Seeds, "Seed de criaturas", "Seed usada para criaturas geradas.", T("Seed")),
        Tok("DIM", General, "Dimensões", "Tamanho do mundo em regiões. Valores válidos: 17, 33, 65, 129, 257.", N("Largura"), N("Altura")),
        Tok("EMBARK_POINTS", General, "Pontos de embarque", "Pontos para comprar itens e habilidades no embarque."),
        Tok("END_YEAR", Civilizations, "Ano final da história", "Quantos anos de história são simulados. Menos anos = mais megabestas vivas."),
        Tok("BEAST_END_YEAR", Civilizations, "Parada por bestas mortas", "A partir do ano indicado, para a história se a % de megabestas mortas for atingida.", N("Ano"), N("% mortas")),
        Tok("REVEAL_ALL_HISTORY", Civilizations, "Revelar toda a história", "Mostra todos os eventos históricos nas lendas.", F()),
        Tok("CULL_HISTORICAL_FIGURES", Civilizations, "Remover figuras sem importância", "Reduz o tamanho do save removendo figuras históricas irrelevantes.", F()),

        Tok("ELEVATION", Terrain, "Elevação", "Faixa de elevação (1–400) e variação ao longo do mapa.", RangeArgs),
        Tok("RAINFALL", Terrain, "Chuva", "Faixa de chuva (0–100) e variação.", RangeArgs),
        Tok("TEMPERATURE", Terrain, "Temperatura", "Faixa de temperatura e variação.", RangeArgs),
        Tok("DRAINAGE", Terrain, "Drenagem", "Faixa de drenagem (0–100). Drenagem baixa favorece pântanos; alta, colinas.", RangeArgs),
        Tok("VOLCANISM", Volcanoes, "Vulcanismo", "Faixa de vulcanismo (0–100). Vulcões só aparecem onde o vulcanismo chega a 100.", RangeArgs),
        Tok("SAVAGERY", Terrain, "Selvageria", "Faixa de selvageria (0–100). Alta = áreas selvagens com criaturas perigosas.", RangeArgs),
        Tok("ELEVATION_FREQUENCY", Terrain, "Frequência de elevação", "Tamanho da malha e pesos das faixas de elevação.", FrequencyArgs),
        Tok("RAIN_FREQUENCY", Terrain, "Frequência de chuva", "Tamanho da malha e pesos das faixas de chuva.", FrequencyArgs),
        Tok("DRAINAGE_FREQUENCY", Terrain, "Frequência de drenagem", "Tamanho da malha e pesos das faixas de drenagem.", FrequencyArgs),
        Tok("TEMPERATURE_FREQUENCY", Terrain, "Frequência de temperatura", "Tamanho da malha e pesos das faixas de temperatura.", FrequencyArgs),
        Tok("SAVAGERY_FREQUENCY", Terrain, "Frequência de selvageria", "Tamanho da malha e pesos das faixas de selvageria.", FrequencyArgs),
        Tok("VOLCANISM_FREQUENCY", Volcanoes, "Frequência de vulcanismo", "Tamanho da malha e pesos das faixas de vulcanismo.", FrequencyArgs),

        Tok("POLE", Terrain, "Polos", "Onde ficam os polos (regiões geladas).", C("Polo", PoleChoices)),
        Tok("MINERAL_SCARCITY", Volcanoes, "Escassez de minerais", "100–20000. Quanto MENOR, mais minério.", N("Escassez")),
        Tok("REAL_WORLD_EXTINCT", Creatures, "Animais extintos do mundo real", "Como tratar animais extintos (mamute etc.).", C("Modo", ExtinctChoices)),
        Tok("MEGABEAST_CAP", Creatures, "Limite de megabestas", "Número máximo de megabestas (dragões, hidras...)."),
        Tok("SEMIMEGABEAST_CAP", Creatures, "Limite de semimegabestas", "Número máximo de semimegabestas (gigantes, ciclopes...)."),
        Tok("TITAN_NUMBER", Creatures, "Número de titãs", "Quantos titãs são gerados."),
        Tok("TITAN_ATTACK_TRIGGER", Creatures, "Gatilho de ataque de titãs", "População/riqueza mínimas para megabestas atacarem a fortaleza.", AttackTrigger),
        Tok("DEMON_NUMBER", Magic, "Tipos de demônios", "Quantos tipos de demônio são gerados."),
        Tok("NIGHT_TROLL_NUMBER", Creatures, "Tipos de trolls noturnos", "Quantos tipos de criaturas noturnas são gerados."),
        Tok("BOGEYMAN_NUMBER", Creatures, "Tipos de bichos-papões", "Quantos tipos de bogeymen são gerados."),
        Tok("NIGHTMARE_NUMBER", Creatures, "Tipos de pesadelos", "Quantos tipos de nightmare são gerados."),
        Tok("VAMPIRE_NUMBER", Creatures, "Maldições de vampiro", "Quantos tipos de maldição vampírica."),
        Tok("WEREBEAST_NUMBER", Creatures, "Maldições de lobisomem", "Quantos tipos de licantropia."),
        Tok("WEREBEAST_ATTACK_TRIGGER", Creatures, "Gatilho de ataque de lobisomens", "População/riqueza mínimas para ataques de lobisomens.", AttackTrigger),
        Tok("SECRET_NUMBER", Magic, "Segredos (necromancia)", "Quantos segredos de vida e morte existem."),
        Tok("REGIONAL_INTERACTION_NUMBER", Magic, "Interações regionais", "Efeitos sobrenaturais regionais (ex.: ressurreição de mortos em áreas malignas)."),
        Tok("DISTURBANCE_INTERACTION_NUMBER", Magic, "Interações de perturbação", "Efeitos de perturbar túmulos e locais."),
        Tok("EVIL_CLOUD_NUMBER", Magic, "Nuvens malignas", "Tipos de nuvens malignas."),
        Tok("EVIL_RAIN_NUMBER", Magic, "Chuvas malignas", "Tipos de chuvas malignas."),
        Tok("GENERATE_DIVINE_MATERIALS", Magic, "Materiais divinos", "Gera materiais divinos.", F()),
        Tok("GENERATE_MYTHICAL_MATERIALS", Magic, "Materiais míticos", "Gera materiais míticos.", F()),
        Tok("ALLOW_MYTHICAL_HEALING", Magic, "Cura mítica", "Permite cura mítica.", F()),
        Tok("ALLOW_DIVINATION", Magic, "Adivinhação", "Permite adivinhação.", F()),
        Tok("ALLOW_DEMONIC_EXPERIMENTS", Magic, "Experimentos demoníacos", "Permite experimentos demoníacos.", F()),
        Tok("ALLOW_NECROMANCER_EXPERIMENTS", Magic, "Experimentos de necromantes", "Permite experimentos de necromantes.", F()),
        Tok("ALLOW_NECROMANCER_LIEUTENANTS", Magic, "Tenentes de necromantes", "Permite tenentes de necromantes.", F()),
        Tok("ALLOW_NECROMANCER_GHOULS", Magic, "Carniçais de necromantes", "Permite ghouls.", F()),
        Tok("ALLOW_NECROMANCER_SUMMONS", Magic, "Invocações de necromantes", "Permite invocações.", F()),

        Tok("GOOD_SQ_COUNTS", Rejection, "Mín. de quadrados bons", "Quantidade mínima de áreas benignas por tamanho de sub-região.", SubregionSizes),
        Tok("EVIL_SQ_COUNTS", Rejection, "Mín. de quadrados malignos", "Quantidade mínima de áreas malignas por tamanho de sub-região.", SubregionSizes),
        Tok("PEAK_NUMBER_MIN", Rejection, "Mín. de picos", "Mínimo de picos de montanha; o mapa é rejeitado se não alcançar."),
        Tok("PARTIAL_OCEAN_EDGE_MIN", Rejection, "Mín. de bordas com oceano parcial", "Quantas bordas do mapa precisam ter oceano parcial."),
        Tok("COMPLETE_OCEAN_EDGE_MIN", Rejection, "Mín. de bordas com oceano completo", "Quantas bordas do mapa precisam ser totalmente oceano."),
        Tok("VOLCANO_MIN", Volcanoes, "Mín. de vulcões", "0–200. O mapa é rejeitado se não tiver vulcões suficientes. Precisa de vulcanismo 100 suficiente."),
        new("REGION_COUNTS", Biomes, "Mínimos por bioma", "Quadrados e regiões mínimos de cada bioma; o mapa é rejeitado se faltar.",
            [C("Bioma", BiomeChoices), N("Mín. quadrados"), N("Mín. regiões (inicial)"), N("Mín. regiões (final)")], IsKeyed: true),

        Tok("EROSION_CYCLE_COUNT", Rivers, "Ciclos de erosão", "Mais ciclos = vales e rios mais marcados (e geração mais lenta)."),
        Tok("RIVER_MINS", Rivers, "Nascentes de rios", "Mínimo de nascentes antes da erosão e quantidade desejada depois.", N("Mín. pré-erosão"), N("Desejado pós-erosão")),
        Tok("PERIODICALLY_ERODE_EXTREMES", Rivers, "Erodir penhascos extremos", "Suaviza periodicamente penhascos extremos.", F()),
        Tok("OROGRAPHIC_PRECIPITATION", Rivers, "Chuva orográfica", "Montanhas geram chuva de um lado e sombra de chuva do outro.", F()),
        Tok("SUBREGION_MAX", Biomes, "Máx. de sub-regiões", "Limite de sub-regiões (biomas contíguos)."),

        Tok("CAVERN_LAYER_COUNT", Underground, "Camadas de caverna", "0–3."),
        Tok("CAVERN_LAYER_OPENNESS_MIN", Underground, "Abertura das cavernas (mín.)", "0–100. Quanto maior, cavernas mais abertas."),
        Tok("CAVERN_LAYER_OPENNESS_MAX", Underground, "Abertura das cavernas (máx.)", "0–100."),
        Tok("CAVERN_LAYER_PASSAGE_DENSITY_MIN", Underground, "Densidade de passagens (mín.)", "0–100."),
        Tok("CAVERN_LAYER_PASSAGE_DENSITY_MAX", Underground, "Densidade de passagens (máx.)", "0–100."),
        Tok("CAVERN_LAYER_WATER_MIN", Underground, "Água nas cavernas (mín.)", "0–100."),
        Tok("CAVERN_LAYER_WATER_MAX", Underground, "Água nas cavernas (máx.)", "0–100."),
        Tok("HAVE_BOTTOM_LAYER_1", Underground, "Mar de magma", "Gera a camada do mar de magma.", F()),
        Tok("HAVE_BOTTOM_LAYER_2", Underground, "Camada do fundo", "Gera a camada mais profunda (força o magma).", F()),
        Tok("LEVELS_ABOVE_GROUND", Underground, "Níveis acima do chão", "Níveis Z acima da superfície."),
        Tok("LEVELS_ABOVE_LAYER_1", Underground, "Níveis acima da camada 1", "Níveis Z de pedra acima da 1ª caverna."),
        Tok("LEVELS_ABOVE_LAYER_2", Underground, "Níveis acima da camada 2", "Níveis Z acima da 2ª caverna."),
        Tok("LEVELS_ABOVE_LAYER_3", Underground, "Níveis acima da camada 3", "Níveis Z acima da 3ª caverna."),
        Tok("LEVELS_ABOVE_LAYER_4", Underground, "Níveis acima da camada 4", "Níveis Z acima do mar de magma."),
        Tok("LEVELS_ABOVE_LAYER_5", Underground, "Níveis acima da camada 5", "Níveis Z acima da camada do fundo."),
        Tok("LEVELS_AT_BOTTOM", Underground, "Níveis no fundo", "Níveis Z da camada do fundo."),
        Tok("CAVE_MIN_SIZE", Underground, "Tamanho mín. de caverna natural", "Cavernas de superfície."),
        Tok("CAVE_MAX_SIZE", Underground, "Tamanho máx. de caverna natural", "Cavernas de superfície."),
        Tok("MOUNTAIN_CAVE_MIN", Underground, "Mín. de cavernas em montanhas", ""),
        Tok("NON_MOUNTAIN_CAVE_MIN", Underground, "Mín. de cavernas fora de montanhas", ""),
        Tok("MYTHICAL_SITE_NUM", Magic, "Locais míticos", "Número de locais míticos."),
        Tok("ALL_CAVES_VISIBLE", Underground, "Cavernas visíveis", "Mostra todas as cavernas no mapa.", F()),
        Tok("SHOW_EMBARK_TUNNEL", Underground, "Mostrar túneis no embarque", "0 = não, 1 = só no buscador, 2 = sempre."),
        Tok("TOTAL_CIV_NUMBER", Civilizations, "Número de civilizações", ""),
        Tok("TOTAL_CIV_POPULATION", Civilizations, "Limite de população após civs", ""),
        Tok("SITE_CAP", Civilizations, "Limite de locais", "Limite de locais (cidades, fortalezas...) após a criação das civs."),
        Tok("PLAYABLE_CIVILIZATION_REQUIRED", Civilizations, "Exigir civ jogável", "Rejeita mundos sem civilização jogável.", F()),

        Tok("ELEVATION_RANGES", Rejection, "Mín. de quadrados por elevação", "Mínimo de quadrados de elevação baixa/média/alta.", LowMidHigh),
        Tok("RAIN_RANGES", Rejection, "Mín. de quadrados por chuva", "Mínimo de quadrados de chuva baixa/média/alta.", LowMidHigh),
        Tok("DRAINAGE_RANGES", Rejection, "Mín. de quadrados por drenagem", "Mínimo de quadrados de drenagem baixa/média/alta.", LowMidHigh),
        Tok("SAVAGERY_RANGES", Rejection, "Mín. de quadrados por selvageria", "Mínimo de quadrados de selvageria baixa/média/alta.", LowMidHigh),
        Tok("VOLCANISM_RANGES", Volcanoes, "Mín. de quadrados por vulcanismo", "Mínimo de quadrados de vulcanismo baixo/médio/alto.", LowMidHigh),

        Painted("PS_EL", "Elevação pintada"),
        Painted("PS_RF", "Chuva pintada"),
        Painted("PS_TP", "Temperatura pintada"),
        Painted("PS_DR", "Drenagem pintada"),
        Painted("PS_VL", "Vulcanismo pintado"),
        Painted("PS_SV", "Selvageria pintada"),
    ];

    private static WorldGenTokenInfo Painted(string name, string display) =>
        new(name, Painter, display, "Dados do World Painter, uma linha por Y. Preservados, mas não editáveis aqui.", [], IsEditable: false);

    private static readonly Dictionary<string, (WorldGenTokenInfo Info, int Order)> ByName =
        All.Select((info, i) => (info, i)).ToDictionary(x => x.info.Name, x => (x.info, x.i), StringComparer.OrdinalIgnoreCase);

    public static WorldGenTokenInfo? Get(string name) => ByName.TryGetValue(name, out var e) ? e.Info : null;

    public static bool IsKeyed(string name) => Get(name)?.IsKeyed ?? false;

    /// <summary>Posição do token na ordem do jogo. Tokens desconhecidos vão para o fim.</summary>
    public static int CanonicalOrder(string name) => ByName.TryGetValue(name, out var e) ? e.Order : int.MaxValue;

    /// <summary>Metadados para um token qualquer, inclusive desconhecido (rótulos genéricos).</summary>
    public static WorldGenTokenInfo Describe(WorldGenToken token)
    {
        if (Get(token.Name) is { } info) return info;
        var args = token.Args.Select((_, i) => new WorldGenArgInfo($"Valor {i + 1}")).ToArray();
        return new WorldGenTokenInfo(token.Name, Other, token.Name, "Token não catalogado (preservado como está).", args);
    }
}
