namespace DwarfVolon.Core.Embark;

/// <summary>Nomes em pt-BR de habilidades, atributos e níveis do Dwarf Fortress.</summary>
public static class DfNames
{
    private static readonly Dictionary<string, string> SkillNames = new()
    {
        ["MINING"] = "Mineração",
        ["WOODCUTTING"] = "Corte de madeira",
        ["CARPENTRY"] = "Carpintaria",
        ["ENGRAVE_STONE"] = "Gravação",
        ["MASONRY"] = "Alvenaria",
        ["ANIMALTRAIN"] = "Adestramento",
        ["ANIMALCARE"] = "Cuidar de animais",
        ["DISSECT_FISH"] = "Dissecar peixes",
        ["DISSECT_VERMIN"] = "Dissecar animais",
        ["PROCESSFISH"] = "Limpar peixes",
        ["BUTCHER"] = "Açougue",
        ["TRAPPING"] = "Armadilhas",
        ["TANNER"] = "Curtume",
        ["WEAVING"] = "Tecelagem",
        ["BREWING"] = "Cervejaria",
        ["CLOTHESMAKING"] = "Costura",
        ["MILLING"] = "Moagem",
        ["PROCESSPLANTS"] = "Debulha",
        ["CHEESEMAKING"] = "Queijaria",
        ["MILK"] = "Ordenha",
        ["COOK"] = "Cozinha",
        ["PLANT"] = "Cultivo",
        ["HERBALISM"] = "Herbalismo",
        ["FISH"] = "Pesca",
        ["SMELT"] = "Operar fornalha",
        ["EXTRACT_STRAND"] = "Extrair fios",
        ["FORGE_WEAPON"] = "Forjar armas",
        ["FORGE_ARMOR"] = "Forjar armaduras",
        ["FORGE_FURNITURE"] = "Ferraria",
        ["CUTGEM"] = "Lapidação",
        ["ENCRUSTGEM"] = "Engaste de gemas",
        ["WOODCRAFT"] = "Artesanato em madeira",
        ["STONECRAFT"] = "Artesanato em pedra",
        ["METALCRAFT"] = "Artesanato em metal",
        ["GLASSMAKER"] = "Vidraria",
        ["LEATHERWORK"] = "Couro",
        ["BONECARVE"] = "Entalhe em osso",
        ["AXE"] = "Machado",
        ["SWORD"] = "Espada",
        ["DAGGER"] = "Faca",
        ["MACE"] = "Maça",
        ["HAMMER"] = "Martelo",
        ["SPEAR"] = "Lança",
        ["CROSSBOW"] = "Besta",
        ["SHIELD"] = "Escudo",
        ["ARMOR"] = "Uso de armadura",
        ["SIEGECRAFT"] = "Engenharia de cerco",
        ["SIEGEOPERATE"] = "Operar armas de cerco",
        ["BOWYER"] = "Fazer arcos",
        ["PIKE"] = "Pique",
        ["WHIP"] = "Chicote",
        ["BOW"] = "Arco",
        ["BLOWGUN"] = "Zarabatana",
        ["THROW"] = "Arremesso",
        ["MECHANICS"] = "Mecânica",
        ["SNEAK"] = "Emboscada",
        ["DRESS_WOUNDS"] = "Curativos",
        ["DIAGNOSE"] = "Diagnóstico",
        ["SURGERY"] = "Cirurgia",
        ["SET_BONE"] = "Imobilizar ossos",
        ["SUTURE"] = "Sutura",
        ["WOOD_BURNING"] = "Queimar madeira",
        ["LYE_MAKING"] = "Fazer lixívia",
        ["SOAP_MAKING"] = "Saboaria",
        ["POTASH_MAKING"] = "Fazer potassa",
        ["DYER"] = "Tinturaria",
        ["OPERATE_PUMP"] = "Operar bomba",
        ["SWIMMING"] = "Natação",
        ["PERSUASION"] = "Persuasão",
        ["NEGOTIATION"] = "Negociação",
        ["JUDGING_INTENT"] = "Julgar intenções",
        ["APPRAISAL"] = "Avaliação",
        ["ORGANIZATION"] = "Organização",
        ["RECORD_KEEPING"] = "Registros",
        ["LYING"] = "Mentir",
        ["INTIMIDATION"] = "Intimidação",
        ["CONVERSATION"] = "Conversa",
        ["COMEDY"] = "Comédia",
        ["FLATTERY"] = "Bajulação",
        ["CONSOLE"] = "Consolar",
        ["PACIFY"] = "Apaziguar",
        ["TRACKING"] = "Rastreamento",
        ["CONCENTRATION"] = "Concentração",
        ["DISCIPLINE"] = "Disciplina",
        ["SITUATIONAL_AWARENESS"] = "Observação",
        ["LEADERSHIP"] = "Liderança",
        ["TEACHING"] = "Ensino",
        ["MELEE_COMBAT"] = "Luta",
        ["RANGED_COMBAT"] = "Tiro",
        ["WRESTLING"] = "Luta corpo a corpo",
        ["DODGING"] = "Esquiva",
        ["MILITARY_TACTICS"] = "Tática militar",
        ["SHEARING"] = "Tosquia",
        ["SPINNING"] = "Fiação",
        ["POTTERY"] = "Cerâmica",
        ["GLAZING"] = "Vitrificação",
        ["PRESSING"] = "Prensagem",
        ["BEEKEEPING"] = "Apicultura",
        ["WAX_WORKING"] = "Trabalho com cera",
        ["CLIMBING"] = "Escalada",
        ["GELD"] = "Castração",
        ["PAPERMAKING"] = "Fazer papel",
        ["BOOKBINDING"] = "Encadernação",
        ["SPEAKING"] = "Oratória",
        ["POETRY"] = "Poesia",
        ["PROSE"] = "Prosa",
        ["WRITING"] = "Escrita",
        ["READING"] = "Leitura",
        ["DANCE"] = "Dança",
        ["MAKE_MUSIC"] = "Música",
        ["SING_MUSIC"] = "Canto",
        ["PLAY_KEYBOARD_INSTRUMENT"] = "Instrumento de teclas",
        ["PLAY_STRINGED_INSTRUMENT"] = "Instrumento de cordas",
        ["PLAY_WIND_INSTRUMENT"] = "Instrumento de sopro",
        ["PLAY_PERCUSSION_INSTRUMENT"] = "Percussão",
        ["CUT_STONE"] = "Cortar pedra",
        ["CARVE_STONE"] = "Esculpir pedra",
    };

    private static readonly Dictionary<string, string> AttributeNames = new()
    {
        ["STRENGTH"] = "Força",
        ["AGILITY"] = "Agilidade",
        ["TOUGHNESS"] = "Robustez",
        ["ENDURANCE"] = "Resistência",
        ["RECUPERATION"] = "Recuperação",
        ["DISEASE_RESISTANCE"] = "Resistência a doenças",
        ["ANALYTICAL_ABILITY"] = "Análise",
        ["FOCUS"] = "Foco",
        ["WILLPOWER"] = "Força de vontade",
        ["CREATIVITY"] = "Criatividade",
        ["INTUITION"] = "Intuição",
        ["PATIENCE"] = "Paciência",
        ["MEMORY"] = "Memória",
        ["LINGUISTIC_ABILITY"] = "Linguagem",
        ["SPATIAL_SENSE"] = "Senso espacial",
        ["MUSICALITY"] = "Musicalidade",
        ["KINESTHETIC_SENSE"] = "Senso cinestésico",
        ["EMPATHY"] = "Empatia",
        ["SOCIAL_AWARENESS"] = "Percepção social",
    };

    private static readonly Dictionary<string, string> FacetNames = new()
    {
        ["LOVE_PROPENSITY"] = "Propensão ao amor",
        ["HATE_PROPENSITY"] = "Propensão ao ódio",
        ["ENVY_PROPENSITY"] = "Inveja",
        ["CHEER_PROPENSITY"] = "Alegria",
        ["DEPRESSION_PROPENSITY"] = "Tendência à depressão",
        ["ANGER_PROPENSITY"] = "Irritabilidade",
        ["ANXIETY_PROPENSITY"] = "Ansiedade",
        ["LUST_PROPENSITY"] = "Luxúria",
        ["STRESS_VULNERABILITY"] = "Vulnerabilidade ao estresse",
        ["GREED"] = "Ganância",
        ["IMMODERATION"] = "Imoderação",
        ["VIOLENT"] = "Violência",
        ["PERSEVERANCE"] = "Perseverança",
        ["WASTEFULNESS"] = "Desperdício",
        ["DISCORD"] = "Discórdia",
        ["FRIENDLINESS"] = "Simpatia",
        ["POLITENESS"] = "Educação",
        ["DISDAIN_ADVICE"] = "Desdém por conselhos",
        ["BRAVERY"] = "Coragem",
        ["CONFIDENCE"] = "Autoconfiança",
        ["VANITY"] = "Vaidade",
        ["AMBITION"] = "Ambição",
        ["GRATITUDE"] = "Gratidão",
        ["IMMODESTY"] = "Imodéstia",
        ["HUMOR"] = "Humor",
        ["VENGEFUL"] = "Espírito vingativo",
        ["PRIDE"] = "Orgulho",
        ["CRUELTY"] = "Crueldade",
        ["SINGLEMINDED"] = "Obstinação",
        ["HOPEFUL"] = "Esperança",
        ["CURIOUS"] = "Curiosidade",
        ["BASHFUL"] = "Timidez",
        ["PRIVACY"] = "Reserva",
        ["PERFECTIONIST"] = "Perfeccionismo",
        ["CLOSEMINDED"] = "Teimosia",
        ["TOLERANT"] = "Tolerância",
        ["EMOTIONALLY_OBSESSIVE"] = "Apego emocional",
        ["SWAYED_BY_EMOTIONS"] = "Levado pelas emoções",
        ["ALTRUISM"] = "Altruísmo",
        ["DUTIFULNESS"] = "Senso de dever",
        ["THOUGHTLESSNESS"] = "Impulsividade",
        ["ORDERLINESS"] = "Organização",
        ["TRUST"] = "Confiança nos outros",
        ["GREGARIOUSNESS"] = "Sociabilidade",
        ["ASSERTIVENESS"] = "Assertividade",
        ["ACTIVITY_LEVEL"] = "Energia",
        ["EXCITEMENT_SEEKING"] = "Busca por emoção",
        ["IMAGINATION"] = "Imaginação",
        ["ABSTRACT_INCLINED"] = "Pensamento abstrato",
        ["ART_INCLINED"] = "Gosto por arte",
    };

    private static readonly Dictionary<string, string> BeliefNames = new()
    {
        ["LAW"] = "Lei",
        ["LOYALTY"] = "Lealdade",
        ["FAMILY"] = "Família",
        ["FRIENDSHIP"] = "Amizade",
        ["POWER"] = "Poder",
        ["TRUTH"] = "Verdade",
        ["CUNNING"] = "Astúcia",
        ["ELOQUENCE"] = "Eloquência",
        ["FAIRNESS"] = "Justiça",
        ["DECORUM"] = "Decoro",
        ["TRADITION"] = "Tradição",
        ["ARTWORK"] = "Arte",
        ["COOPERATION"] = "Cooperação",
        ["INDEPENDENCE"] = "Independência",
        ["STOICISM"] = "Estoicismo",
        ["INTROSPECTION"] = "Introspecção",
        ["SELF_CONTROL"] = "Autocontrole",
        ["TRANQUILITY"] = "Tranquilidade",
        ["HARMONY"] = "Harmonia",
        ["MERRIMENT"] = "Festa",
        ["CRAFTSMANSHIP"] = "Artesanato",
        ["MARTIAL_PROWESS"] = "Habilidade marcial",
        ["SKILL"] = "Maestria",
        ["HARD_WORK"] = "Trabalho duro",
        ["SACRIFICE"] = "Sacrifício",
        ["COMPETITION"] = "Competição",
        ["PERSEVERANCE"] = "Perseverança",
        ["LEISURE_TIME"] = "Lazer",
        ["COMMERCE"] = "Comércio",
        ["ROMANCE"] = "Romance",
        ["NATURE"] = "Natureza",
        ["PEACE"] = "Paz",
        ["KNOWLEDGE"] = "Conhecimento",
    };

    private static readonly Dictionary<string, string> DreamNames = new()
    {
        ["STAY_ALIVE"] = "Sobreviver",
        ["MAINTAIN_ENTITY_STATUS"] = "Manter a posição",
        ["START_A_FAMILY"] = "Formar uma família",
        ["RULE_THE_WORLD"] = "Governar o mundo",
        ["CREATE_A_GREAT_WORK_OF_ART"] = "Criar uma grande obra de arte",
        ["CRAFT_A_MASTERWORK"] = "Fazer uma obra-prima",
        ["BRING_PEACE_TO_THE_WORLD"] = "Trazer paz ao mundo",
        ["BECOME_A_LEGENDARY_WARRIOR"] = "Ser um guerreiro lendário",
        ["MASTER_A_SKILL"] = "Dominar uma habilidade",
        ["FALL_IN_LOVE"] = "Se apaixonar",
        ["SEE_THE_GREAT_NATURAL_SITES"] = "Ver as grandes maravilhas naturais",
        ["IMMORTALITY"] = "Imortalidade",
        ["MAKE_A_GREAT_DISCOVERY"] = "Fazer uma grande descoberta",
        ["ATTAINING_RANK_IN_SOCIETY"] = "Subir na sociedade",
        ["BATHING_WORLD_IN_CHAOS"] = "Mergulhar o mundo no caos",
    };

    private static readonly Dictionary<string, string> PreferenceKindNames = new()
    {
        ["LikeMaterial"] = "Materiais",
        ["LikeFood"] = "Comida e bebida",
        ["LikeCreature"] = "Criaturas",
        ["HateCreature"] = "Odeia",
        ["LikeItem"] = "Itens",
        ["LikePlant"] = "Plantas",
        ["LikeTree"] = "Árvores",
        ["LikeColor"] = "Cores",
        ["LikeShape"] = "Formas",
        ["LikePoeticForm"] = "Poesia",
        ["LikeMusicalForm"] = "Música",
        ["LikeDanceForm"] = "Dança",
    };

    /// <summary>Facetas de personalidade, na ordem do jogo.</summary>
    public static IReadOnlyList<string> Facets => [.. FacetNames.Keys];

    public static string Facet(string id) => FacetNames.GetValueOrDefault(id, Humanize(id));

    public static string Belief(string id) => BeliefNames.GetValueOrDefault(id, Humanize(id));

    public static string Dream(string id) => DreamNames.GetValueOrDefault(id, Humanize(id));

    /// <summary>Grupo de um gosto (ex.: LikeMaterial → Materiais).</summary>
    public static string PreferenceKind(string kind) => PreferenceKindNames.GetValueOrDefault(kind, Humanize(kind));

    /// <summary>Ordem de exibição dos grupos de gostos (ódios por último).</summary>
    public static int PreferenceKindOrder(string kind)
    {
        var index = PreferenceKindNames.Keys.ToList().IndexOf(kind);
        return kind == "HateCreature" ? 1000 : index < 0 ? 999 : index;
    }

    private static string Humanize(string id) =>
        id.Length == 0 ? id : char.ToUpperInvariant(id[0]) + id[1..].ToLowerInvariant().Replace('_', ' ');

    /// <summary>Atributos físicos, na ordem do jogo.</summary>
    public static IReadOnlyList<string> PhysicalAttributes { get; } =
        ["STRENGTH", "AGILITY", "TOUGHNESS", "ENDURANCE", "RECUPERATION", "DISEASE_RESISTANCE"];

    /// <summary>Atributos mentais, na ordem do jogo.</summary>
    public static IReadOnlyList<string> MentalAttributes { get; } =
    [
        "ANALYTICAL_ABILITY", "FOCUS", "WILLPOWER", "CREATIVITY", "INTUITION", "PATIENCE", "MEMORY",
        "LINGUISTIC_ABILITY", "SPATIAL_SENSE", "MUSICALITY", "KINESTHETIC_SENSE", "EMPATHY", "SOCIAL_AWARENESS",
    ];

    private static readonly string[] LevelNames =
    [
        "Iniciante", "Novato", "Adequado", "Competente", "Habilidoso", "Proficiente", "Talentoso", "Perito",
        "Especialista", "Profissional", "Consumado", "Ótimo", "Mestre", "Alto mestre", "Grão-mestre", "Lendário",
    ];

    /// <summary>Nome da habilidade em pt-BR; sem tradução, usa o nome do jogo e, por fim, o id.</summary>
    public static string Skill(string id, IReadOnlyDictionary<string, string>? captions = null) =>
        SkillNames.TryGetValue(id, out var name) ? name
        : captions is not null && captions.TryGetValue(id, out var caption) && !string.IsNullOrEmpty(caption) ? caption
        : id;

    public static string Attribute(string id) => AttributeNames.GetValueOrDefault(id, id);

    /// <summary>Nome do nível de habilidade (1 = Novato, 5 = Proficiente, 15+ = Lendário).</summary>
    public static string SkillLevel(int level) =>
        level <= 0 ? "-" : level >= 15 ? LevelNames[15] : LevelNames[level];

    public static string Sex(int? sex) => sex switch
    {
        0 => "F",
        1 => "M",
        _ => "?",
    };
}
