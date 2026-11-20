using CommunityToolkit.Mvvm.ComponentModel;
using DwarfVolon.Core.Embark;

namespace DwarfVolon.ViewModels;

/// <summary>Uma linha de gráfico de barras: rótulo, barra e valor.</summary>
public sealed record BarRow(
    string Label,
    double Value,
    string ValueText,
    string Tooltip,
    double Minimum = 0,
    double Maximum = 100,
    double? Origin = null,
    double? Reference = null,
    bool IsHighlighted = false,
    string? Note = null)
{
    /// <summary>Barras de magnitude não destacadas ficam mais claras (ex.: funções não sugeridas).</summary>
    public bool IsMuted => Origin is null && !IsHighlighted;
    public bool HasNote => !string.IsNullOrEmpty(Note);
}

/// <summary>Um grupo de gostos (ex.: Materiais: iron, steel).</summary>
public sealed record PreferenceGroup(string Kind, string Items, bool IsHate);

/// <summary>
/// Ficha de um anão do embarque, pronta para a tela: textos e linhas de gráfico já calculados
/// a partir da análise e do texto capturado das fichas do jogo.
/// </summary>
public sealed partial class DwarfSheetViewModel : ObservableObject, IDisposable
{
    private readonly EmbarkAnalysis _analysis;

    /// <summary>Faixa neutra das facetas: o jogo não descreve valores entre 40 e 60.</summary>
    private const int NeutralLow = 40, NeutralHigh = 60;

    public DwarfSheetViewModel(EmbarkAnalysis analysis, EmbarkDwarf dwarf, EmbarkSheetCache sheets, EmbarkPlan? plan = null,
        Func<IReadOnlyList<EmbarkDwarf>, Task<string?>>? applyToGame = null)
    {
        _analysis = analysis;
        Plan = new PlanEditorViewModel(analysis, dwarf, plan ?? new EmbarkPlan(), applyToGame);
        Plan.RolesChanged += () => RoleBars = BuildRoleBars();
        var s = analysis.Snapshot;
        Dwarf = dwarf;
        Name = dwarf.DisplayName;

        var age = s.AgeOf(dwarf);
        Subtitle = string.Join(" · ", new[]
        {
            dwarf.NameEnglish != dwarf.Name ? dwarf.NameEnglish : null,
            string.IsNullOrWhiteSpace(dwarf.Nickname) ? null : $"\"{dwarf.Nickname}\"",
            dwarf.Sex switch { 0 => "fêmea", 1 => "macho", _ => null },
            age is { } a ? $"{a} anos" : null,
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        PicksText = $"{dwarf.PicksLeft} pontos de habilidade livres";
        Alerts = string.Join(", ", EmbarkAnalyzer.PersonalityNotes(dwarf));

        PhysicalBars = DfNames.PhysicalAttributes.Select(a => AttributeRow(analysis, dwarf, a)).OfType<BarRow>().ToList();
        MentalBars = DfNames.MentalAttributes.Select(a => AttributeRow(analysis, dwarf, a)).OfType<BarRow>().ToList();

        RoleBars = BuildRoleBars();

        FacetBars = dwarf.Traits
            .Where(t => t.Value < NeutralLow || t.Value > NeutralHigh)
            .OrderByDescending(t => Math.Abs(t.Value - 50))
            .Select(t => new BarRow(DfNames.Facet(t.Key), t.Value, t.Value.ToString(),
                $"{DfNames.Facet(t.Key)}: {t.Value} (0–100, neutro entre 40 e 60)", Origin: 50))
            .ToList();

        BeliefBars = dwarf.Beliefs
            .OrderByDescending(b => Math.Abs(b.Strength))
            .Select(b => new BarRow(DfNames.Belief(b.Id), b.Strength, b.Strength.ToString("+0;-0;0"),
                $"{DfNames.Belief(b.Id)}: {b.Strength:+0;-0;0} (-50 = despreza, 50 = valoriza muito)",
                Minimum: -50, Maximum: 50, Origin: 0))
            .ToList();

        Dreams = string.Join(", ", dwarf.Dreams.Select(DfNames.Dream));

        PreferenceGroups = dwarf.Preferences
            .GroupBy(p => p.Kind)
            .OrderBy(g => DfNames.PreferenceKindOrder(g.Key))
            .Select(g => new PreferenceGroup(
                DfNames.PreferenceKind(g.Key),
                string.Join(", ", g.Select(p => string.IsNullOrWhiteSpace(p.Text) ? "(sem nome)" : p.Text)),
                g.Key == "HateCreature"))
            .ToList();

        BackgroundSkills = dwarf.BackgroundSkills.Select(k => $"{DfNames.Skill(k.Id, s.SkillCaptions)} {k.Level}").ToList();
        ChosenSkills = dwarf.Skills.Select(k => $"{DfNames.Skill(k.Id, s.SkillCaptions)} {k.Level}").ToList();

        var captured = sheets.For(dwarf.UnitId);
        SheetParagraphs = captured.SelectMany(c => c.Paragraphs).ToList();
        SheetInfo = captured.Count == 0
            ? "Abra a ficha deste anão no jogo, na aba de personalidade, com a atualização automática ligada (ou clique em Atualizar). O texto do jogo aparece aqui e fica guardado."
            : $"Capturado da ficha do jogo às {captured.Max(c => c.CapturedAt):HH:mm:ss}" +
              (captured.Count > 1 ? $" ({captured.Count} abas)." : ". Outras abas de personalidade abertas no jogo também entram aqui.");
    }

    public EmbarkDwarf Dwarf { get; }

    /// <summary>Editor do plano de pontos deste anão.</summary>
    public PlanEditorViewModel Plan { get; }

    public void Dispose() => Plan.Dispose();
    public string Name { get; }
    public string Subtitle { get; }
    public string PicksText { get; }
    public string Alerts { get; }
    public bool HasAlerts => Alerts.Length > 0;

    public IReadOnlyList<BarRow> PhysicalBars { get; }
    public IReadOnlyList<BarRow> MentalBars { get; }
    /// <summary>Aptidão por função; em destaque, as funções que o anão tem no plano.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BarRow> RoleBars { get; private set; }

    private IReadOnlyList<BarRow> BuildRoleBars()
    {
        var dwarf = Dwarf;
        var chosen = Plan.RoleIds.ToHashSet();
        return _analysis.Roles
            .Select(r => (Role: r, Fit: _analysis.Fit(dwarf, r), Rank: Rank(_analysis, dwarf, r)))
            .OrderByDescending(x => x.Fit.Score)
            .Select(x => new BarRow(
                x.Role.Name, x.Fit.Score, x.Fit.Score.ToString(),
                RoleTooltip(x.Fit, x.Rank, _analysis.Snapshot.Dwarves.Count),
                Reference: 50,
                IsHighlighted: chosen.Contains(x.Role.Id),
                Note: x.Rank == 1 ? "melhor" : $"{x.Rank}º"))
            .ToList();
    }
    public IReadOnlyList<BarRow> FacetBars { get; }
    public IReadOnlyList<BarRow> BeliefBars { get; }
    public bool HasFacets => FacetBars.Count > 0;
    public bool HasBeliefs => BeliefBars.Count > 0;

    public string Dreams { get; }
    public bool HasDreams => Dreams.Length > 0;

    public IReadOnlyList<PreferenceGroup> PreferenceGroups { get; }
    public bool HasPreferences => PreferenceGroups.Count > 0;

    public IReadOnlyList<string> BackgroundSkills { get; }
    public IReadOnlyList<string> ChosenSkills { get; }
    public bool HasChosenSkills => ChosenSkills.Count > 0;

    public IReadOnlyList<DfTextParagraph> SheetParagraphs { get; }
    public bool HasSheetText => SheetParagraphs.Count > 0;
    public string SheetInfo { get; }

    private static BarRow? AttributeRow(EmbarkAnalysis analysis, EmbarkDwarf dwarf, string attribute)
    {
        if (dwarf.Attribute(attribute) is not { } raw) return null;
        var score = analysis.AttributeScore(dwarf, attribute);
        var median = analysis.Snapshot.AttributeRanges.GetValueOrDefault(attribute) is { Count: 7 } r ? r[3] : (int?)null;
        var tooltip = $"{DfNames.Attribute(attribute)}: {raw}" +
                      (median is { } m ? $" (mediana da raça {m})" : "") +
                      $" · nota {score} de 100";
        return new BarRow(DfNames.Attribute(attribute), score, score.ToString(), tooltip, Origin: 50);
    }

    private static int Rank(EmbarkAnalysis analysis, EmbarkDwarf dwarf, EmbarkRole role)
    {
        var mine = analysis.Fit(dwarf, role).Score;
        return 1 + analysis.Snapshot.Dwarves.Count(d => d.Index != dwarf.Index && analysis.Fit(d, role).Score > mine);
    }

    private static string RoleTooltip(RoleFit fit, int rank, int total)
    {
        var attributes = string.Join(", ", fit.Attributes.Select(a => $"{DfNames.Attribute(a.Attribute)} {a.Score}"));
        return $"{fit.Role.Name}: nota {fit.Score} · {rank}º de {total} anões\n{attributes}";
    }
}
