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
