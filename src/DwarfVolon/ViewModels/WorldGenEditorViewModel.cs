using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwarfVolon.Core.DwarfFortress;
using DwarfVolon.Core.Settings;
using DwarfVolon.Core.WorldGen;
using DwarfVolon.Services;

namespace DwarfVolon.ViewModels;

public sealed record BaseOptionViewModel(string Title, string Detail, string Source, WorldGenParameterSet Set)
{
    public const string FromUserFile = "user";
    public const string FromBuiltIn = "builtin";

    public string Id => $"{Source}:{Title}";
}

/// <summary>
/// Gerador de mundo. Fluxo: base → um nível por categoria de modificador → edições manuais
/// (aba Avançado) → título → salvar. Toda mudança de base ou de nível reconstrói o conjunto
/// de trabalho; as edições manuais ficam guardadas e são reaplicadas por cima.
/// </summary>
public partial class WorldGenEditorViewModel : ViewModelBase
{
    public const int SimpleTab = 0;
    public const int AdvancedTab = 1;
    private static readonly (int, int) CalibratedSize = (129, 129);

    private static readonly HashSet<string> ExpandedByDefault =
        [WorldGenTokenCatalog.General, WorldGenTokenCatalog.Rivers, WorldGenTokenCatalog.Volcanoes];

    private readonly Dictionary<(string Name, string? Key), WorldGenToken> _manualEdits = [];
    private WorldGenParameterSet? _working;
    private bool _titleEditedByUser;
    private bool _settingTitle;
    private bool _batchUpdate;
    private HashSet<string> _userTitles = new(StringComparer.OrdinalIgnoreCase);

    public WorldGenEditorViewModel()
    {
        LoadModifiers();
        LoadBases();
    }

    // ---------- Tela simples ----------

    public ObservableCollection<ModifierCategoryViewModel> Categories { get; } = [];
    public ObservableCollection<ProfileViewModel> Profiles { get; } = [];
    public ObservableCollection<SummaryLine> SummaryLines { get; } = [];

    [ObservableProperty]
    public partial string Warnings { get; private set; } = "";

    public bool HasWarnings => Warnings.Length > 0;

    /// <summary>Estimativa de risco de rejeição de mapas (ver <see cref="WorldGenRiskEstimator"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRiskLow), nameof(IsRiskMedium), nameof(IsRiskHigh))]
    public partial RiskLevel Risk { get; private set; }

    [ObservableProperty]
    public partial string RiskText { get; private set; } = "";

    [ObservableProperty]
    public partial string RiskDetails { get; private set; } = "";

    public bool IsRiskLow => Risk == RiskLevel.Low;
    public bool IsRiskMedium => Risk == RiskLevel.Medium;
    public bool IsRiskHigh => Risk == RiskLevel.High;

    public bool HasNoChanges => SummaryLines.Count == 0 && _manualEdits.Count == 0;

    public string ManualEditsText => _manualEdits.Count == 0 ? "" : $"+ {_manualEdits.Count} ajuste(s) manual(is) na aba Avançado";

    public bool HasManualEdits => _manualEdits.Count > 0;

    public string BaseText => SelectedBase is null ? "-" : $"{SelectedBase.Title} ({FormatDim(SelectedBase.Set)})";

    public bool IsCalibratedBase => SelectedBase?.Set.Dimensions == CalibratedSize;

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    // ---------- Aba Avançado ----------

    public ObservableCollection<BaseOptionViewModel> BaseOptions { get; } = [];
    public ObservableCollection<TokenGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BaseText), nameof(IsCalibratedBase))]
    public partial BaseOptionViewModel? SelectedBase { get; set; }

    [ObservableProperty]
    public partial string PreviewText { get; private set; } = "";

    [ObservableProperty]
    public partial string ChangeLog { get; private set; } = "";

    [ObservableProperty]
    public partial string SummaryText { get; private set; } = "";

    /// <summary>Resumo do mundo em Markdown (aba Markdown e botão "Copiar Markdown").</summary>
    [ObservableProperty]
    public partial string MarkdownSummary { get; private set; } = "";

    /// <summary>Catálogo de categorias e perfis em Markdown, para dar contexto a uma IA.</summary>
    public string MarkdownCatalog => WorldGenMarkdown.DescribeCatalog(Categories.Select(c => c.Category), Profiles.Select(p => p.Profile));

    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    [ObservableProperty]
    public partial bool OnlyChanged { get; set; }

    // ---------- Salvar ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleWarning), nameof(HasTitleWarning))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string NewTitle { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetFileText))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial DwarfFortressInstallation? Installation { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    public string TargetFileText => Installation is null
        ? "Configure a pasta do Dwarf Fortress em Configurações para poder salvar."
        : $"Arquivo: {Installation.WorldGenFilePath}";

    public string TitleWarning =>
        string.IsNullOrWhiteSpace(NewTitle) ? "Informe um título." :
        _userTitles.Contains(WorldGenParameterSet.SanitizeText(NewTitle))
            ? "Já existe um conjunto com esse título no seu world_gen.txt. Salvar vai substituí-lo (com backup)."
            : "";

    public bool HasTitleWarning => TitleWarning.Length > 0;

    /// <summary>Chamado depois de salvar (para a tela de configurações atualizar o status).</summary>
    public event Action? Saved;

    partial void OnInstallationChanged(DwarfFortressInstallation? value) => LoadBases();

    partial void OnSelectedBaseChanged(BaseOptionViewModel? value) => Rebuild();

    partial void OnNewTitleChanged(string value)
    {
        if (!_settingTitle) _titleEditedByUser = !string.IsNullOrWhiteSpace(value);
        if (_working is null) return;
        _working.Title = string.IsNullOrWhiteSpace(value) ? null : value;
        UpdatePreview();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnOnlyChangedChanged(bool value) => ApplyFilter();

    // ---------- Comandos ----------

    [RelayCommand]
    private void ResetAll() => ApplyProfile(new WorldGenProfile { Id = "padrao", Name = "Tudo padrão" });

    [RelayCommand]
    private void ReloadModifiers()
    {
        var selected = Categories.ToDictionary(c => c.Id, c => c.SelectedOption.Option.Id);
        LoadModifiers();
        _batchUpdate = true;
        foreach (var category in Categories)
            category.SelectSilently(selected.GetValueOrDefault(category.Id));
        _batchUpdate = false;
        Rebuild();
    }

    [RelayCommand]
    private void ReloadBases() => LoadBases();

    [RelayCommand]
    private void ClearManualEdits()
    {
        _manualEdits.Clear();
        Rebuild();
        StatusMessage = "Ajustes manuais desfeitos.";
    }

    [RelayCommand]
    private void ShowAdvanced() => SelectedTabIndex = AdvancedTab;

    [RelayCommand]
    private void OpenUserModifiersFolder() => ShellHelper.OpenFolder(AppPaths.UserModifiersDirectory);

    [RelayCommand]
    private void OpenPrefsFolder()
    {
        if (Installation is not null) ShellHelper.OpenFolder(Installation.PrefsDirectory);
    }

    private bool CanSave() => Installation is not null && !string.IsNullOrWhiteSpace(NewTitle);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (_working is null || Installation is null) return;
        if (Groups.SelectMany(g => g.Rows).FirstOrDefault(r => r.HasErrors) is { } invalid)
        {
            StatusMessage = $"Corrija o valor inválido em \"{invalid.DisplayName}\" (aba Avançado) antes de salvar.";
            return;
        }

        try
        {
            var result = new WorldGenRepository(Installation.WorldGenFilePath).Save(_working);
            var title = _working.Title;
            StatusMessage = (result.Replaced ? $"✔ \"{title}\" atualizado." : $"✔ \"{title}\" salvo.") +
                            (result.BackupPath is null ? "" : $" Backup: {Path.GetFileName(result.BackupPath)}.");
            LoadBases();
            Saved?.Invoke();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Erro ao salvar: {ex.Message}";
        }
    }

    // ---------- Carga ----------

    private void LoadModifiers()
    {
        var errors = new List<string>();
        var categories = ModifierLibrary.Merge(
            ModifierLibrary.LoadBuiltInCategories(),
            ModifierLibrary.LoadCategoriesFromDirectory(AppPaths.UserModifiersDirectory, errors));

        Categories.Clear();
        foreach (var category in categories) Categories.Add(new ModifierCategoryViewModel(category, OnModifierChanged));

        Profiles.Clear();
        foreach (var profile in ModifierLibrary.LoadBuiltInProfiles()) Profiles.Add(new ProfileViewModel(profile, ApplyProfile));

        if (errors.Count > 0) StatusMessage = "Modificadores com erro: " + string.Join("; ", errors);
        OnPropertyChanged(nameof(MarkdownCatalog));
    }

    private void LoadBases()
    {
        var previousId = SelectedBase?.Id;
        var options = new List<BaseOptionViewModel>();
        _userTitles = new(StringComparer.OrdinalIgnoreCase);

        if (Installation is not null)
        {
            try
            {
                var userDoc = new WorldGenRepository(Installation.WorldGenFilePath).Load();
                foreach (var set in userDoc.ParameterSets.Where(s => s.Title is not null))
                {
                    _userTitles.Add(set.Title!);
                    options.Add(new BaseOptionViewModel(set.Title!, $"Seu world_gen.txt • {FormatDim(set)}", BaseOptionViewModel.FromUserFile, set));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusMessage = $"Não consegui ler o world_gen.txt: {ex.Message}";
            }
        }

        foreach (var set in BuiltInTemplates.Load().ParameterSets.Where(s => s.Title is not null))
            options.Add(new BaseOptionViewModel(set.Title!, $"Padrão do jogo • {FormatDim(set)}", BaseOptionViewModel.FromBuiltIn, set));

        BaseOptions.Clear();
        foreach (var option in options) BaseOptions.Add(option);

        SelectedBase = BaseOptions.FirstOrDefault(o => o.Id == previousId)
                       ?? BaseOptions.FirstOrDefault(o => o.Source == BaseOptionViewModel.FromBuiltIn && o.Title == BuiltInTemplates.DefaultTitle)
                       ?? BaseOptions.FirstOrDefault();
        OnPropertyChanged(nameof(TitleWarning));
        OnPropertyChanged(nameof(HasTitleWarning));
    }

    private static string FormatDim(WorldGenParameterSet set) =>
        set.Dimensions is { } d ? $"{d.Width}×{d.Height}" : "tamanho ?";

    // ---------- Montagem do conjunto ----------

    private void ApplyProfile(WorldGenProfile profile)
    {
        _batchUpdate = true;
        foreach (var category in Categories)
            category.SelectSilently(profile.Selections.GetValueOrDefault(category.Id));
        _batchUpdate = false;
        Rebuild();
    }

    private void OnModifierChanged()
    {
        if (!_batchUpdate) Rebuild();
    }

    private void Rebuild()
    {
        if (SelectedBase is null)
        {
            _working = null;
            Groups.Clear();
            PreviewText = ChangeLog = SummaryText = "";
            return;
        }

        var picks = Categories.Where(c => c.IsModified).ToList();
        var working = SelectedBase.Set.Clone();
        var result = PresetApplier.Apply(working, picks.Select(c => c.SelectedOption.Option.ToPreset(c.Category)));
        foreach (var token in _manualEdits.Values) working.Set(token);

        if (!_titleEditedByUser) SetSuggestedTitle(picks);
        working.Title = string.IsNullOrWhiteSpace(NewTitle) ? null : NewTitle;
        _working = working;

        UpdateSummary(picks, result);
        UpdateRisk();
        BuildGroups();
        ChangeLog = FormatChangeLog(picks, result);
        UpdatePreview();
    }

    private void UpdateRisk()
    {
        if (_working is null) return;
        var items = WorldGenRiskEstimator.Estimate(_working);
        Risk = WorldGenRiskEstimator.Overall(items);
        RiskText = Risk switch
        {
            RiskLevel.Low => "Risco de rejeição: baixo",
            RiskLevel.Medium => "Risco de rejeição: médio",
            _ => "Risco de rejeição: alto",
        };
        var attention = items.Where(i => i.Level > RiskLevel.Low).ToList();
        RiskDetails = attention.Count == 0
            ? "Os mínimos exigidos cabem com folga na distribuição esperada (estimativa)."
            : string.Join(Environment.NewLine, attention.Select(i => $"• {i.Label}: {i.Detail}"));
    }

    private void SetSuggestedTitle(IReadOnlyList<ModifierCategoryViewModel> picks)
    {
        var tags = picks.Select(c => c.SelectedOption.Option.TitleTag).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        var name = tags.Count == 0 ? $"{SelectedBase!.Title} PADRAO" : $"{SelectedBase!.Title} {string.Join(' ', tags)}";
        _settingTitle = true;
        NewTitle = WorldGenParameterSet.SanitizeText(name);
        _settingTitle = false;
    }

    private void UpdateSummary(IReadOnlyList<ModifierCategoryViewModel> picks, PresetApplyResult result)
    {
        SummaryLines.Clear();
        foreach (var category in picks) SummaryLines.Add(new SummaryLine(category.Icon, category.Name, category.SelectedOption.Name));

        var notes = picks.SelectMany(c => c.SelectedOption.Option.Notes).Concat(result.Warnings).ToList();
        if (!IsCalibratedBase) notes.Insert(0, "Os modificadores foram calibrados para mundos médios (129×129); nesta base o efeito pode ser diferente.");
        Warnings = string.Join(Environment.NewLine, notes.Select(n => "⚠ " + n));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(HasNoChanges));
        OnPropertyChanged(nameof(ManualEditsText));
        OnPropertyChanged(nameof(HasManualEdits));

        foreach (var profile in Profiles)
        {
            profile.IsActive = Categories.All(c =>
                string.Equals(c.SelectedOption.Option.Id,
                    profile.Profile.Selections.GetValueOrDefault(c.Id) ?? c.Category.DefaultOption.Id,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    private void BuildGroups()
    {
        var baseSet = SelectedBase!.Set;
        var rows = _working!.Tokens
            .Where(t => t.Name != "TITLE")
            .Select(t => (Token: t, Info: WorldGenTokenCatalog.Describe(t)))
            .Where(x => x.Info.IsEditable)
            .Select(x => (x.Info.Category, Row: new TokenRowViewModel(x.Token, baseSet.Find(x.Token.Name, x.Token.Key), x.Info, OnRowEdited)))
            .GroupBy(x => x.Category, x => x.Row);

        var wasExpanded = Groups.ToDictionary(g => g.Name, g => g.IsExpanded);
        Groups.Clear();
        foreach (var group in rows.OrderBy(g => IndexOfCategory(g.Key)))
        {
            Groups.Add(new TokenGroupViewModel(group.Key, group)
            {
                IsExpanded = wasExpanded.TryGetValue(group.Key, out var expanded) ? expanded : ExpandedByDefault.Contains(group.Key),
            });
        }
        ApplyFilter();
    }

    private static int IndexOfCategory(string category)
    {
        var index = WorldGenTokenCatalog.CategoryOrder.ToList().IndexOf(category);
        return index < 0 ? int.MaxValue : index;
    }

    private void OnRowEdited(TokenRowViewModel row)
    {
        if (_working is null) return;
        var token = row.ToToken();
        _working.Set(token);
        _manualEdits[(token.Name, token.Key)] = token;
        UpdateRisk();
        UpdatePreview();
        OnPropertyChanged(nameof(ManualEditsText));
        OnPropertyChanged(nameof(HasManualEdits));
        OnPropertyChanged(nameof(HasNoChanges));
        if (OnlyChanged) ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filter = FilterText.Trim();
        foreach (var group in Groups)
        {
            foreach (var row in group.Rows)
                row.IsVisible = (filter.Length == 0 || row.Matches(filter)) && (!OnlyChanged || row.IsChanged);
            group.IsVisible = group.Rows.Any(r => r.IsVisible);
            if (filter.Length > 0 || OnlyChanged) group.IsExpanded = group.IsVisible;
            group.RefreshHeader();
        }
        UpdateCounts();
    }

    private void UpdatePreview()
    {
        PreviewText = _working?.ToText(Environment.NewLine) ?? "";
        MarkdownSummary = _working is null || SelectedBase is null
            ? ""
            : WorldGenMarkdown.Describe(_working, SelectedBase.Set, Categories.Select(c => (c.Category, c.SelectedOption.Option)));
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        var all = Groups.SelectMany(g => g.Rows).ToList();
        SummaryText = $"{all.Count(r => r.IsChanged)} de {all.Count} parâmetros diferentes da base";
    }

    private string FormatChangeLog(IReadOnlyList<ModifierCategoryViewModel> picks, PresetApplyResult result)
    {
        var sb = new StringBuilder();
        if (picks.Count == 0) sb.AppendLine("Todas as categorias no padrão.");

        foreach (var category in picks)
        {
            var presetName = category.SelectedOption.Option.ToPreset(category.Category).Name;
            sb.AppendLine($"■ {presetName}");
            foreach (var change in result.Changes.Where(c => c.PresetName == presetName))
                sb.AppendLine($"    {change.TokenText,-34} {change.Before ?? "(novo)"}  →  {change.After}");
            sb.AppendLine();
        }

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine("Avisos:");
            foreach (var warning in result.Warnings) sb.AppendLine($"    {warning}");
            sb.AppendLine();
        }

        if (_manualEdits.Count > 0)
        {
            sb.AppendLine("Ajustes manuais (aplicados por cima):");
            foreach (var token in _manualEdits.Values) sb.AppendLine($"    {token.ToText()}");
        }

        return sb.ToString().TrimEnd();
    }
}
