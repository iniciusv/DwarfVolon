using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DwarfVolon.Core.Markdown;

namespace DwarfVolon.Core.Embark;

/// <summary>De onde veio um plano.</summary>
public enum PlanSource
{
    /// <summary>Montado à mão no editor.</summary>
    Manual,

    /// <summary>Botão "Sugerir" (heurística do app).</summary>
    AppSuggestion,

    /// <summary>Resposta da IA colada.</summary>
    Ai,
}

/// <summary>Origem do plano de um anão: quem montou, quando, e se foi ajustado à mão depois.</summary>
public sealed record PlanOrigin(PlanSource Source, DateTime At, bool EditedByHand = false)
{
    public string Describe() => Source switch
    {
        PlanSource.Ai => $"Veio da resposta da IA colada às {At:HH:mm}" + (EditedByHand ? ", com ajustes seus." : "."),
        PlanSource.AppSuggestion => "Funções sugeridas pelo app" + (EditedByHand ? ", com ajustes seus." : "."),
        _ => "Montado à mão.",
    };
}

/// <summary>
/// Decisões do embarque para cada anão: as <b>funções</b> (ex.: Minerador, que depois do embarque viram
/// trabalhos designados) e o <b>plano de pontos</b> (nível de cada habilidade comprada).
/// Nada aqui é automático: vem do editor da ficha, do botão Sugerir ou de uma resposta da IA colada.
/// </summary>
public sealed class EmbarkPlan
{
    private readonly Dictionary<int, Dictionary<string, int>> _levels = [];
    private readonly Dictionary<int, List<string>> _roles = [];
    private readonly Dictionary<int, PlanOrigin> _origins = [];

    /// <summary>Avisa quando algum anão muda.</summary>
    public event Action<EmbarkDwarf>? Changed;

    /// <summary>Tem algo decidido (pontos ou funções) para o anão.</summary>
    public bool Has(EmbarkDwarf dwarf) => For(dwarf).Count > 0 || RolesOf(dwarf).Count > 0;

    /// <summary>Níveis planejados do anão (vazio se não há plano).</summary>
    public IReadOnlyDictionary<string, int> For(EmbarkDwarf dwarf) =>
        _levels.TryGetValue(Key(dwarf), out var levels) ? levels : new Dictionary<string, int>();

    /// <summary>Funções do anão (ids do catálogo de funções), na ordem em que foram dadas.</summary>
    public IReadOnlyList<string> RolesOf(EmbarkDwarf dwarf) =>
        _roles.TryGetValue(Key(dwarf), out var roles) ? roles : [];

    /// <summary>De onde veio o plano do anão (nulo se não há nada).</summary>
    public PlanOrigin? OriginOf(EmbarkDwarf dwarf) => Has(dwarf) ? _origins.GetValueOrDefault(Key(dwarf)) : null;

    /// <summary>Muda um nível à mão; 0 tira a habilidade do plano.</summary>
    public void Set(EmbarkDwarf dwarf, string skill, int level)
    {
        var levels = Levels(dwarf);
        if (level <= 0) levels.Remove(skill);
        else levels[skill] = level;
        TouchByHand(dwarf);
    }

    /// <summary>Troca os pontos do anão (e as funções, se vierem).</summary>
    public void Replace(EmbarkDwarf dwarf, IReadOnlyDictionary<string, int> levels, PlanSource source = PlanSource.Manual,
        IReadOnlyList<string>? roles = null)
    {
        _levels[Key(dwarf)] = levels.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        if (roles is not null) _roles[Key(dwarf)] = roles.Distinct().ToList();
        _origins[Key(dwarf)] = new PlanOrigin(source, DateTime.Now);
        Changed?.Invoke(dwarf);
    }

    /// <summary>Troca as funções do anão (sem mexer nos pontos).</summary>
    public void SetRoles(EmbarkDwarf dwarf, IReadOnlyList<string> roles, PlanSource source = PlanSource.Manual)
    {
        _roles[Key(dwarf)] = roles.Distinct().ToList();
        if (source == PlanSource.Manual) TouchByHand(dwarf, notify: false);
        else _origins[Key(dwarf)] = new PlanOrigin(source, DateTime.Now);
        Changed?.Invoke(dwarf);
    }

    public void AddRole(EmbarkDwarf dwarf, string role)
    {
        if (RolesOf(dwarf).Contains(role)) return;
        SetRoles(dwarf, [.. RolesOf(dwarf), role]);
    }

    public void RemoveRole(EmbarkDwarf dwarf, string role) => SetRoles(dwarf, RolesOf(dwarf).Where(r => r != role).ToList());

    /// <summary>Limpa os pontos do anão (as funções ficam).</summary>
    public void ClearPoints(EmbarkDwarf dwarf)
    {
        if (_levels.Remove(Key(dwarf))) Changed?.Invoke(dwarf);
    }

    /// <summary>Restaura um anão salvo (sem marcar como editado).</summary>
    public void Restore(EmbarkDwarf dwarf, IReadOnlyDictionary<string, int> levels, IReadOnlyList<string> roles, PlanOrigin origin)
    {
        _levels[Key(dwarf)] = levels.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        _roles[Key(dwarf)] = roles.Distinct().ToList();
        _origins[Key(dwarf)] = origin;
        Changed?.Invoke(dwarf);
    }

    /// <summary>Pontos que o plano do anão gasta.</summary>
    public int Cost(EmbarkDwarf dwarf) => For(dwarf).Values.Sum(EmbarkPlanRules.LevelCost);

    private Dictionary<string, int> Levels(EmbarkDwarf dwarf)
    {
        if (!_levels.TryGetValue(Key(dwarf), out var levels)) _levels[Key(dwarf)] = levels = [];
        return levels;
    }

    private void TouchByHand(EmbarkDwarf dwarf, bool notify = true)
    {
        var key = Key(dwarf);
        _origins[key] = _origins.TryGetValue(key, out var origin) && origin.Source != PlanSource.Manual
            ? origin with { EditedByHand = true }
            : new PlanOrigin(PlanSource.Manual, DateTime.Now);
        if (notify) Changed?.Invoke(dwarf);
    }

    // UnitId identifica o anão mesmo se a ordem mudar; sem ele, usa a posição.
    private static int Key(EmbarkDwarf dwarf) => dwarf.UnitId ?? -1 - dwarf.Index;
}

/// <summary>
/// Regras do embarque para o plano. Ainda supostas: 1 ponto por nível, até 5 (Proficiente) por
/// habilidade, limite = pontos livres do anão + níveis já escolhidos. A conferir no jogo.
/// </summary>
public static class EmbarkPlanRules
{
    public static int MaxLevel => EmbarkAnalyzer.MaxEmbarkLevel;

    public static int LevelCost(int level) => Math.Max(0, level);

    /// <summary>Pontos de habilidade que o anão pode gastar no total.</summary>
    public static int Budget(EmbarkDwarf dwarf) => dwarf.PicksLeft + dwarf.SpentLevels;

    /// <summary>Habilidades que a tela de embarque oferece (todas as abas), na ordem do jogo.</summary>
    public static IReadOnlyList<string> AllowedSkills(EmbarkSnapshot snapshot) =>
        snapshot.SkillTabs.Values.SelectMany(t => t).Distinct().ToList();

    /// <summary>Problemas do plano de um anão (vazio = ok).</summary>
    public static IReadOnlyList<string> Check(EmbarkSnapshot snapshot, EmbarkDwarf dwarf, IReadOnlyDictionary<string, int> levels)
    {
        var problems = new List<string>();
        var cost = levels.Values.Sum(LevelCost);
        var budget = Budget(dwarf);
        if (cost > budget) problems.Add($"{dwarf.DisplayName}: o plano gasta {cost} pontos, mas o limite é {budget}.");
        foreach (var (skill, level) in levels.Where(kv => kv.Value > MaxLevel))
            problems.Add($"{dwarf.DisplayName}: {DfNames.Skill(skill, snapshot.SkillCaptions)} {level} passa do máximo ({MaxLevel}).");
        var allowed = AllowedSkills(snapshot);
        if (allowed.Count > 0)
            foreach (var skill in levels.Keys.Where(k => !allowed.Contains(k)))
                problems.Add($"{dwarf.DisplayName}: {DfNames.Skill(skill, snapshot.SkillCaptions)} não está na tela de embarque.");
        return problems;
    }

    /// <summary>O que muda no jogo se o plano do anão for aplicado (vazio = já está igual).</summary>
    public static IReadOnlyList<string> Diff(EmbarkSnapshot snapshot, EmbarkDwarf dwarf, IReadOnlyDictionary<string, int> levels)
    {
        var current = dwarf.Skills.ToDictionary(k => k.Id, k => k.Level);
        return current.Keys.Union(levels.Keys)
            .Select(id => (Id: id, From: current.GetValueOrDefault(id), To: levels.GetValueOrDefault(id)))
            .Where(x => x.From != x.To)
            .OrderByDescending(x => x.To).ThenBy(x => x.Id)
            .Select(x => $"{DfNames.Skill(x.Id, snapshot.SkillCaptions)} {x.From} → {x.To}")
            .ToList();
    }
}

/// <summary>Resultado de ler uma resposta da IA.</summary>
public sealed class EmbarkPlanImport
{
    /// <summary>Níveis por anão (posição na lista), só dos anões que a resposta trouxe.</summary>
    public Dictionary<int, Dictionary<string, int>> Levels { get; } = [];

    /// <summary>Funções por anão, só das linhas que trouxeram "| funções: ...".</summary>
    public Dictionary<int, List<string>> Roles { get; } = [];

    /// <summary>Linhas ou itens que não deu para entender (não foram usados).</summary>
    public List<string> Errors { get; } = [];

    /// <summary>Coisas usadas, mas fora das regras (ex.: passa do limite).</summary>
    public List<string> Warnings { get; } = [];

    public bool FoundBlock { get; internal set; }
}

/// <summary>
/// Protocolo de conversa com a IA. O pedido (separado do resumo) leva o que o jogador quer, as regras e o
/// formato exato da resposta; a resposta traz um bloco de código <c>dwarfvolon-pontos</c>, uma linha por anão:
/// <code>
/// Besmar Ustuthkifed: SET_BONE 5, SURGERY 4, CARPENTRY 1 | funções: Médico, Carpinteiro
/// </code>
/// O nome é o da tabela; a habilidade é o ID do jogo (nomes em pt-BR ou inglês também são aceitos); a parte
/// "| funções:" é opcional. Anão que aparece tem os pontos (e as funções, se vierem) trocados; os outros ficam.
/// </summary>
public static partial class EmbarkAiProtocol
{
    public const string PointsBlock = "dwarfvolon-pontos";

    private static readonly Dictionary<string, string> TabNames = new()
    {
        ["CRUCIAL"] = "Essenciais",
        ["LABOR"] = "Trabalho",
        ["COMBAT"] = "Combate",
        ["OTHER"] = "Outras",
    };

    /// <summary>
    /// Pedido para a IA distribuir os pontos e definir as funções: o que se quer, as regras e o formato da
    /// resposta. Não leva o resumo do embarque: o jogador pode mandar o resumo antes, conversar, e só depois
    /// mandar este pedido. Os nomes e limites dos anões vão junto para o pedido valer sozinho.
    /// </summary>
    public static string BuildPointsRequest(EmbarkAnalysis analysis, string? instructions)
    {
        var s = analysis.Snapshot;
        var md = new MarkdownBuilder();

        md.Heading(1, "Pedido: funções e pontos de habilidade")
          .Paragraph("Defina as funções de cada anão e distribua os pontos de habilidade do embarque (os dados estão no resumo " +
                     "do DwarfVolon que mandei nesta conversa), levando em conta o que conversamos.");
        if (!string.IsNullOrWhiteSpace(instructions))
            md.Heading(2, "Instruções do jogador").Quote(instructions.Trim());

        md.Heading(2, "Regras")
          .Bullets([
              $"Cada nível custa 1 ponto. Nível de 1 a {EmbarkPlanRules.MaxLevel} por habilidade ({DfNames.SkillLevel(EmbarkPlanRules.MaxLevel)}).",
              "Limite de pontos de cada anão: " +
              string.Join(", ", s.Dwarves.Select(d => $"{d.DisplayName} {EmbarkPlanRules.Budget(d)}")) + ".",
              "As habilidades de \"Já sabe\" não gastam pontos e não precisam ser repetidas.",
              "Use só as habilidades da lista abaixo, pelo ID.",
              "Funções: 1 ou 2 por anão, da lista de funções. Depois do embarque, só quem tem a função faz aquele trabalho " +
              "(ex.: só os mineradores mineram).",
              "Anão que aparecer na resposta tem pontos e funções trocados pela lista; anão que não aparecer fica como está.",
          ])
          .Heading(2, "Funções disponíveis")
          .Paragraph(string.Join(", ", analysis.Roles.Select(r => r.Name)) + ".")
          .Heading(2, "Habilidades disponíveis (ID)");
        var tabOrder = TabNames.Keys.ToList();
        md.Bullets(s.SkillTabs
            .OrderBy(t => tabOrder.IndexOf(t.Key) is var i and >= 0 ? i : int.MaxValue)
            .Select(t => $"{TabNames.GetValueOrDefault(t.Key, t.Key)}: {string.Join(", ", t.Value)}"));

        var example = s.Dwarves.Count > 0 ? s.Dwarves[0].DisplayName : "Urist McAnão";
        md.Heading(2, "Formato da resposta")
          .Paragraph($"Responda com **um** bloco de código `{PointsBlock}`, uma linha por anão, com o nome exatamente como na tabela, " +
                     "as habilidades como `ID nível` separadas por vírgula e, depois de `| funções:`, as funções. Exemplo:")
          .Code($"{example}: SKILL_ID 3, OUTRA_SKILL 2 | funções: Função A, Função B", PointsBlock)
          .Paragraph("Depois do bloco, explique as escolhas em poucas linhas.");

        return md.ToString();
    }

    /// <summary>As decisões atuais no formato do protocolo (para copiar, guardar ou mandar de volta para a IA).</summary>
    public static string Format(EmbarkSnapshot snapshot, EmbarkPlan plan, IReadOnlyList<EmbarkRole>? roles = null)
    {
        roles ??= EmbarkRoles.Default;
        var sb = new StringBuilder($"```{PointsBlock}\n");
        foreach (var dwarf in snapshot.Dwarves.Where(plan.Has))
        {
            var levels = plan.For(dwarf).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key);
            sb.Append(dwarf.DisplayName).Append(": ").AppendJoin(", ", levels.Select(kv => $"{kv.Key} {kv.Value}"));
            var names = plan.RolesOf(dwarf).Select(id => roles.FirstOrDefault(r => r.Id == id)?.Name ?? id).ToList();
            if (names.Count > 0) sb.Append(" | funções: ").AppendJoin(", ", names);
            sb.Append('\n');
        }
        return sb.Append("```\n").ToString();
    }

    [GeneratedRegex(@"```[ \t]*" + PointsBlock + @"[^\n]*\n(?<body>.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BlockRegex();

    [GeneratedRegex(@"^(?<skill>.*?)[\s:=]+(?<level>-?\d+)$")]
    private static partial Regex ItemRegex();

    [GeneratedRegex(@"\|\s*fun[çc](?:ões|oes|ão|ao)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex RolesMarker();

    /// <summary>
    /// Lê a resposta da IA. Procura o bloco <c>dwarfvolon-pontos</c>; sem ele, tenta o texto todo
    /// (para quando a IA esquece as crases).
    /// </summary>
    public static EmbarkPlanImport Parse(string response, EmbarkSnapshot snapshot, IReadOnlyList<EmbarkRole>? roles = null)
    {
        roles ??= EmbarkRoles.Default;
        var result = new EmbarkPlanImport();
        var block = BlockRegex().Match(response ?? "");
        result.FoundBlock = block.Success;
        var body = block.Success ? block.Groups["body"].Value : response ?? "";

        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim().TrimStart('-', '*', '•').Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("```")) continue;

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                if (block.Success) result.Errors.Add($"Linha sem \"nome: habilidades\": {line}");
                continue;
            }

            var name = line[..colon].Trim().Trim('*').Trim();
            var rest = line[(colon + 1)..];
            string? rolesPart = null;
            if (RolesMarker().Match(rest) is { Success: true } marker)
            {
                rolesPart = rest[(marker.Index + marker.Length)..];
                rest = rest[..marker.Index];
            }

            var dwarf = FindDwarf(snapshot, name);
            if (dwarf is null)
            {
                if (block.Success || rolesPart is not null || LooksLikePlan(rest)) result.Errors.Add($"Anão não encontrado: {name}");
                continue;
            }

            var levels = new Dictionary<string, int>();
            foreach (var item in rest.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var m = ItemRegex().Match(item);
                var skill = m.Success ? FindSkill(snapshot, m.Groups["skill"].Value) : null;
                if (skill is null)
                {
                    result.Errors.Add($"{dwarf.DisplayName}: não entendi \"{item}\".");
                    continue;
                }
                var level = int.Parse(m.Groups["level"].Value, CultureInfo.InvariantCulture);
                if (level > EmbarkPlanRules.MaxLevel)
                {
                    result.Warnings.Add($"{dwarf.DisplayName}: {DfNames.Skill(skill, snapshot.SkillCaptions)} {level} reduzido para {EmbarkPlanRules.MaxLevel}.");
                    level = EmbarkPlanRules.MaxLevel;
                }
                if (level > 0) levels[skill] = level;
            }

            if (rolesPart is not null)
            {
                var found = new List<string>();
                foreach (var item in rolesPart.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (FindRole(roles, item) is { } role) found.Add(role.Id);
                    else result.Errors.Add($"{dwarf.DisplayName}: função desconhecida \"{item}\".");
                }
                result.Roles[dwarf.Index] = found.Distinct().ToList();
            }

            if (result.Levels.ContainsKey(dwarf.Index))
                result.Warnings.Add($"{dwarf.DisplayName} apareceu mais de uma vez; ficou a última linha.");
            result.Levels[dwarf.Index] = levels;
            result.Warnings.AddRange(EmbarkPlanRules.Check(snapshot, dwarf, levels));
        }

        if (result.Levels.Count == 0 && result.Errors.Count == 0)
            result.Errors.Add($"Não achei nenhum plano. A resposta precisa ter um bloco `{PointsBlock}`.");
        return result;
    }

    /// <summary>Aplica o que foi lido no plano (só os anões que vieram na resposta).</summary>
    public static void Apply(EmbarkPlanImport import, EmbarkSnapshot snapshot, EmbarkPlan plan)
    {
        foreach (var (index, levels) in import.Levels)
            if (snapshot.Dwarves.FirstOrDefault(d => d.Index == index) is { } dwarf)
                plan.Replace(dwarf, levels, PlanSource.Ai, import.Roles.GetValueOrDefault(index));
    }

    private static bool LooksLikePlan(string text) => ItemRegex().IsMatch(text.Split(',')[0].Trim());

    internal static EmbarkRole? FindRole(IReadOnlyList<EmbarkRole> roles, string text)
    {
        var key = Normalize(text);
        return roles.FirstOrDefault(r => Normalize(r.Id) == key || Normalize(r.Name) == key)
               ?? roles.FirstOrDefault(r => Normalize(r.Name).StartsWith(key, StringComparison.Ordinal) && key.Length >= 4);
    }

    private static EmbarkDwarf? FindDwarf(EmbarkSnapshot snapshot, string name)
    {
        var key = Normalize(name);
        var exact = snapshot.Dwarves.Where(d => Normalize(d.DisplayName) == key || Normalize(d.NameEnglish) == key).ToList();
        if (exact.Count == 1) return exact[0];

        // Só o primeiro nome, se não for ambíguo.
        var first = snapshot.Dwarves.Where(d => Normalize(d.DisplayName).Split(' ')[0] == key.Split(' ')[0]).ToList();
        return first.Count == 1 ? first[0] : null;
    }

    private static string? FindSkill(EmbarkSnapshot snapshot, string text)
    {
        var id = Regex.Replace(text.Trim().Trim('`', '*'), @"[\s-]+", "_").ToUpperInvariant();
        var allowed = EmbarkPlanRules.AllowedSkills(snapshot);
        if (allowed.Contains(id) || snapshot.SkillCaptions.ContainsKey(id)) return id;

        var key = Normalize(text);
        return allowed.Concat(snapshot.SkillCaptions.Keys).Distinct().FirstOrDefault(s =>
            Normalize(DfNames.Skill(s)) == key ||
            (snapshot.SkillCaptions.TryGetValue(s, out var caption) && Normalize(caption) == key));
    }

    /// <summary>Minúsculas, sem acentos e com espaços simples (nomes do DF têm ï, è, ö...).</summary>
    internal static string Normalize(string text)
    {
        var decomposed = text.Trim().Trim('`', '*', '"').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return Regex.Replace(sb.ToString(), @"\s+", " ");
    }
}
