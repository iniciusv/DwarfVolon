namespace DwarfVolon.Core.Embark;

/// <summary>Um atributo do anão e a nota dele (0–100, 50 = mediana da raça).</summary>
public sealed record AttributeScore(string Attribute, int Value, int Score);

/// <summary>Quanto um anão combina com uma função.</summary>
public sealed record RoleFit(EmbarkRole Role, EmbarkDwarf Dwarf, int Score, int CurrentLevel, IReadOnlyList<AttributeScore> Attributes)
{
    /// <summary>Atributos da função em que o anão vai bem (nota ≥ 60), do melhor para o pior.</summary>
    public IReadOnlyList<AttributeScore> Strengths =>
        Attributes.Where(a => a.Score >= 60).OrderByDescending(a => a.Score).ToList();

    /// <summary>Atributos da função em que o anão vai mal (nota ≤ 40).</summary>
    public IReadOnlyList<AttributeScore> Weaknesses =>
        Attributes.Where(a => a.Score <= 40).OrderBy(a => a.Score).ToList();
}

/// <summary>Função sugerida para um anão, com o nível a comprar.</summary>
public sealed record RoleAssignment(RoleFit Fit, bool IsPrimary, int SuggestedLevel)
{
    public EmbarkRole Role => Fit.Role;
    public EmbarkDwarf Dwarf => Fit.Dwarf;
}

public sealed class EmbarkAnalysis
{
    private readonly Dictionary<(int Dwarf, string Role), RoleFit> _fits;

    internal EmbarkAnalysis(EmbarkSnapshot snapshot, IReadOnlyList<EmbarkRole> roles, Dictionary<(int, string), RoleFit> fits,
        IReadOnlyList<RoleAssignment> assignments, IReadOnlyList<string> warnings)
    {
        Snapshot = snapshot;
        Roles = roles;
        _fits = fits;
        Assignments = assignments;
        Warnings = warnings;
    }

    public EmbarkSnapshot Snapshot { get; }
    public IReadOnlyList<EmbarkRole> Roles { get; }

    /// <summary>Sugestão de funções, na ordem de prioridade das funções.</summary>
    public IReadOnlyList<RoleAssignment> Assignments { get; }

    public IReadOnlyList<string> Warnings { get; }

    public RoleFit Fit(EmbarkDwarf dwarf, EmbarkRole role) => _fits[(dwarf.Index, role.Id)];

    public IReadOnlyList<RoleAssignment> AssignmentsFor(EmbarkDwarf dwarf) =>
        Assignments.Where(a => a.Dwarf.Index == dwarf.Index).OrderByDescending(a => a.IsPrimary).ToList();

    /// <summary>Melhor anão para a função (maior nota).</summary>
    public RoleFit Best(EmbarkRole role) =>
        Snapshot.Dwarves.Select(d => Fit(d, role)).OrderByDescending(f => f.Score).ThenBy(f => f.Dwarf.Index).First();

    /// <summary>Nota (0–100) de um atributo do anão.</summary>
    public int AttributeScore(EmbarkDwarf dwarf, string attribute) =>
        dwarf.Attribute(attribute) is { } value
            ? EmbarkAnalyzer.ScoreAttribute(value, Snapshot.AttributeRanges.GetValueOrDefault(attribute))
            : 0;
}

/// <summary>
/// Analisa os anões do embarque: nota de cada anão em cada função (pelos atributos) e uma
/// sugestão de quem faz o quê e com quantos níveis. É uma heurística simples e explicável;
/// a IA pode refinar a partir das mesmas tabelas.
/// </summary>
public static class EmbarkAnalyzer
{
    /// <summary>Nível máximo que dá para comprar no embarque (Proficiente).</summary>
    public const int MaxEmbarkLevel = 5;

    /// <summary>Funções por anão: uma principal e uma secundária.</summary>
    public const int MaxRolesPerDwarf = 2;

    /// <summary>
    /// Nota 0–100 de um atributo. Com a faixa da casta (7 pontos, o 4º é a mediana), interpola:
    /// mínimo = 0, mediana = 50, máximo = 100. Sem faixa, compara com 1000 (valor típico).
    /// </summary>
    public static int ScoreAttribute(int value, IReadOnlyList<int>? range)
    {
        if (range is not { Count: 7 } || range[6] <= range[0])
            return Math.Clamp((int)Math.Round(50 + (value - 1000) / 20.0), 0, 100);

        if (value <= range[0]) return 0;
        if (value >= range[6]) return 100;
        for (var k = 0; k < 6; k++)
        {
            if (value > range[k + 1]) continue;
            var span = range[k + 1] - range[k];
            var t = k + (span == 0 ? 1 : (value - range[k]) / (double)span);
            return (int)Math.Round(t / 6 * 100);
        }
        return 100;
    }

    public static EmbarkAnalysis Analyze(EmbarkSnapshot snapshot, IReadOnlyList<EmbarkRole>? roles = null)
    {
        roles ??= EmbarkRoles.Default;
        var warnings = new List<string>();
        if (snapshot.AttributeRanges.Count == 0)
            warnings.Add("O jogo não informou as faixas de atributos da raça; as notas usam valores típicos.");
        if (snapshot.Dwarves.Count > 0 && snapshot.Dwarves.All(d => d.Physical.Count == 0 && d.Mental.Count == 0))
            warnings.Add("Os atributos dos anões não vieram do jogo; as notas por função não são confiáveis.");

        var fits = new Dictionary<(int, string), RoleFit>();
        foreach (var dwarf in snapshot.Dwarves)
        foreach (var role in roles)
        {
            var attributes = role.Attributes
                .Select(a => dwarf.Attribute(a) is { } v
                    ? new AttributeScore(a, v, ScoreAttribute(v, snapshot.AttributeRanges.GetValueOrDefault(a)))
                    : null)
                .OfType<AttributeScore>()
                .ToList();
            var score = attributes.Count == 0 ? 0 : (int)Math.Round(attributes.Average(a => a.Score));
            fits[(dwarf.Index, role.Id)] = new RoleFit(role, dwarf, score, dwarf.SkillLevel(role.Skill), attributes);
        }

        var assignments = Assign(snapshot.Dwarves, roles, fits);
        return new EmbarkAnalysis(snapshot, roles, fits, assignments, warnings);
    }

    /// <summary>
    /// Distribui as vagas (funções × quantidade, por prioridade) entre os anões, em rodadas: na 1ª, as
    /// vagas mais prioritárias, uma por anão (função principal); na 2ª, as seguintes (secundária).
    /// Em cada rodada a escolha é a melhor combinação possível (algoritmo húngaro), não "o melhor
    /// para a primeira vaga": assim um anão ótimo em duas coisas não é gasto na primeira que aparece.
    /// Funções mais prioritárias pesam um pouco mais, e quem já tem a habilidade ganha preferência.
    /// </summary>
    private static List<RoleAssignment> Assign(IReadOnlyList<EmbarkDwarf> dwarves, IReadOnlyList<EmbarkRole> roles,
        Dictionary<(int, string), RoleFit> fits)
    {
        var result = new List<RoleAssignment>();
        if (dwarves.Count == 0) return result;

        var slots = roles.OrderBy(r => r.Priority)
            .SelectMany(r => Enumerable.Repeat(r, Math.Max(1, r.Count)))
            .Take(dwarves.Count * MaxRolesPerDwarf)
            .ToList();
        var maxPriority = slots.Count == 0 ? 0 : slots.Max(r => r.Priority);

        var taken = dwarves.ToDictionary(d => d.Index, _ => new List<RoleFit>());
        for (var start = 0; start < slots.Count; start += dwarves.Count)
        {
            var round = slots.Skip(start).Take(dwarves.Count).ToList();
            var cost = new double[round.Count, dwarves.Count];
            for (var i = 0; i < round.Count; i++)
            for (var j = 0; j < dwarves.Count; j++)
            {
                var fit = fits[(dwarves[j].Index, round[i].Id)];
                var already = taken[dwarves[j].Index].Any(f => f.Role.Id == round[i].Id);
                var benefit = (fit.Score + (fit.CurrentLevel > 0 ? 10 + 3 * fit.CurrentLevel : 0))
                              * (1 + 0.08 * (maxPriority - round[i].Priority));
                cost[i, j] = already ? 1e6 : -benefit;
            }

            var columns = Hungarian(cost);
            for (var i = 0; i < round.Count; i++)
            {
                // Custo proibitivo = o único anão que sobrou já tem essa função: a vaga fica vazia.
                if (cost[i, columns[i]] >= 1e6) continue;
                var dwarf = dwarves[columns[i]];
                taken[dwarf.Index].Add(fits[(dwarf.Index, round[i].Id)]);
            }
        }

        foreach (var dwarf in dwarves)
        {
            var mine = taken[dwarf.Index];
            var levels = DistributeLevels(dwarf.PicksLeft + dwarf.SpentLevels, mine.Count);
            for (var i = 0; i < mine.Count; i++)
                result.Add(new RoleAssignment(mine[i], i == 0, levels[i]));
        }

        var order = slots.Select(r => r.Id).Distinct().ToList();
        return result.OrderBy(a => order.IndexOf(a.Role.Id)).ThenByDescending(a => a.Fit.Score).ToList();
    }

    /// <summary>
    /// Atribuição de custo mínimo (algoritmo húngaro, O(n²m)). Linhas = vagas, colunas = anões,
    /// com linhas ≤ colunas. Devolve, para cada linha, a coluna escolhida.
    /// </summary>
    internal static int[] Hungarian(double[,] cost)
    {
        int n = cost.GetLength(0), m = cost.GetLength(1);
        if (n > m) throw new ArgumentException("Mais vagas que anões numa rodada.", nameof(cost));

        var u = new double[n + 1];
        var v = new double[m + 1];
        var p = new int[m + 1];
        var way = new int[m + 1];
        for (var i = 1; i <= n; i++)
        {
            p[0] = i;
            var j0 = 0;
            var minv = Enumerable.Repeat(double.PositiveInfinity, m + 1).ToArray();
            var used = new bool[m + 1];
            do
            {
                used[j0] = true;
                int i0 = p[j0], j1 = 0;
                var delta = double.PositiveInfinity;
                for (var j = 1; j <= m; j++)
                {
                    if (used[j]) continue;
                    var cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j])
                    {
                        minv[j] = cur;
                        way[j] = j0;
                    }
                    if (minv[j] < delta)
                    {
                        delta = minv[j];
                        j1 = j;
                    }
                }
                for (var j = 0; j <= m; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }
                j0 = j1;
            } while (p[j0] != 0);

            do
            {
                var j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }

        var result = new int[n];
        for (var j = 1; j <= m; j++)
            if (p[j] != 0) result[p[j] - 1] = j - 1;
        return result;
    }

    /// <summary>
    /// Divide os pontos de habilidade entre as funções: 1 nível para cada uma (se der) e o resto
    /// enche a principal até <see cref="MaxEmbarkLevel"/>, depois a secundária.
    /// </summary>
    public static int[] DistributeLevels(int budget, int roleCount)
    {
        var levels = new int[roleCount];
        for (var i = 0; i < roleCount && budget > 0; i++, budget--) levels[i] = 1;
        for (var i = 0; i < roleCount && budget > 0; i++)
        {
            var add = Math.Min(MaxEmbarkLevel - levels[i], budget);
            levels[i] += add;
            budget -= add;
        }
        return levels;
    }

    /// <summary>Traços de personalidade que vale saber no embarque (ex.: estressa fácil).</summary>
    public static IReadOnlyList<string> PersonalityNotes(EmbarkDwarf dwarf)
    {
        var notes = new List<string>();
        void Check(string facet, string? high, string? low)
        {
            if (!dwarf.Traits.TryGetValue(facet, out var v)) return;
            if (v >= 76 && high is not null) notes.Add(high);
            else if (v <= 24 && low is not null) notes.Add(low);
        }

        Check("STRESS_VULNERABILITY", "estressa fácil", "aguenta estresse");
        Check("ANXIETY_PROPENSITY", "ansioso", null);
        Check("DEPRESSION_PROPENSITY", "tende à depressão", null);
        Check("ANGER_PROPENSITY", "irritável", null);
        Check("BRAVERY", "corajoso", "medroso");
        Check("CHEER_PROPENSITY", "alegre", null);
        Check("PERSEVERANCE", "persistente", null);
        Check("ACTIVITY_LEVEL", "agitado", "parado");
        return notes;
    }
}
