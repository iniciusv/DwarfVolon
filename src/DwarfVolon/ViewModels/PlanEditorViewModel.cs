using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwarfVolon.Core.Embark;

namespace DwarfVolon.ViewModels;

/// <summary>Habilidade que pode entrar no plano (para a lista de "adicionar").</summary>
public sealed record SkillOption(string Id, string Name, string Group)
{
    public override string ToString() => $"{Name} · {Group}";
}

/// <summary>Uma habilidade do plano, com botões de − e +.</summary>
public partial class PlanRowViewModel(PlanEditorViewModel owner, string skillId, string name, int level) : ObservableObject
{
    public string SkillId { get; } = skillId;
    public string Name { get; } = name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LevelName))]
    [NotifyCanExecuteChangedFor(nameof(IncreaseCommand), nameof(DecreaseCommand))]
    public partial int Level { get; set; } = level;

    public string LevelName => DfNames.SkillLevel(Level);

    private bool CanIncrease() => Level < EmbarkPlanRules.MaxLevel;

    [RelayCommand(CanExecute = nameof(CanIncrease))]
    private void Increase() => owner.SetLevel(SkillId, Level + 1);

    private bool CanDecrease() => Level > 1;

    [RelayCommand(CanExecute = nameof(CanDecrease))]
    private void Decrease() => owner.SetLevel(SkillId, Level - 1);

    [RelayCommand]
    private void Remove() => owner.SetLevel(SkillId, 0);
}

/// <summary>Uma função do anão (selo), com ✕ para tirar.</summary>
public sealed partial class RoleChipViewModel(PlanEditorViewModel owner, string id, string name) : ObservableObject
{
    public string Id { get; } = id;
    public string Name { get; } = name;

    [RelayCommand]
    private void Remove() => owner.RemoveRole(Id);
}

/// <summary>
/// Editor das decisões de um anão (na ficha): funções (selos) e plano de pontos. Os dados ficam no
/// <see cref="EmbarkPlan"/> da página (salvo em disco), então sobrevivem às releituras do jogo.
/// Nada é preenchido sozinho: só pelos botões (Sugerir, Adicionar) ou colando uma resposta da IA.
/// </summary>
public partial class PlanEditorViewModel : ObservableObject, IDisposable
{
    private static readonly Dictionary<string, string> TabNames = new()
    {
        ["CRUCIAL"] = "Essenciais",
        ["LABOR"] = "Trabalho",
        ["COMBAT"] = "Combate",
        ["OTHER"] = "Outras",
    };

    private readonly EmbarkAnalysis _analysis;
    private readonly EmbarkDwarf _dwarf;
    private readonly EmbarkPlan _plan;
    private readonly Func<IReadOnlyList<EmbarkDwarf>, Task<string?>>? _applyToGame;

    /// <param name="applyToGame">Aplica no jogo; devolve a mensagem de erro ou nulo se deu certo. Nulo = sem jogo.</param>
    public PlanEditorViewModel(EmbarkAnalysis analysis, EmbarkDwarf dwarf, EmbarkPlan plan,
        Func<IReadOnlyList<EmbarkDwarf>, Task<string?>>? applyToGame = null)
    {
        _analysis = analysis;
        _dwarf = dwarf;
        _plan = plan;
        _applyToGame = applyToGame;

        var s = analysis.Snapshot;
        AvailableSkills = s.SkillTabs
            .SelectMany(t => t.Value.Select(id => new SkillOption(id, DfNames.Skill(id, s.SkillCaptions), TabNames.GetValueOrDefault(t.Key, t.Key))))
            .DistinctBy(o => o.Id)
            .ToList();
        AvailableRoles = analysis.Roles;

        _plan.Changed += OnPlanChanged;
        Reload();
    }

    /// <summary>Avisa a ficha quando as funções mudam (para destacar as barras de aptidão).</summary>
    public event Action? RolesChanged;

    public ObservableCollection<PlanRowViewModel> Rows { get; } = [];
    public ObservableCollection<RoleChipViewModel> Roles { get; } = [];

    public IReadOnlyList<SkillOption> AvailableSkills { get; }
    public IReadOnlyList<EmbarkRole> AvailableRoles { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial SkillOption? NewSkill { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddRoleCommand))]
    public partial EmbarkRole? NewRole { get; set; }

    [ObservableProperty]
    public partial string BudgetText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsOverBudget { get; private set; }

    [ObservableProperty]
    public partial string Problems { get; private set; } = "";

    [ObservableProperty]
    public partial bool HasRows { get; private set; }

    [ObservableProperty]
    public partial bool HasRoles { get; private set; }

    /// <summary>Funções em texto (para a lista de anões).</summary>
    [ObservableProperty]
    public partial string RolesText { get; private set; } = "";

    /// <summary>De onde veio o plano (IA, sugestão do app ou à mão).</summary>
    [ObservableProperty]
    public partial string OriginText { get; private set; } = "";

    [ObservableProperty]
    public partial bool HasOrigin { get; private set; }

    [ObservableProperty]
    public partial bool IsFromAi { get; private set; }

    /// <summary>Comparação do plano com o que está escolhido no jogo.</summary>
    [ObservableProperty]
    public partial string GameStatusText { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool IsInSyncWithGame { get; private set; }

    /// <summary>Mudanças que serão feitas no jogo (mostradas antes de confirmar).</summary>
    [ObservableProperty]
    public partial string PendingChanges { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(ConfirmApplyCommand))]
    public partial bool IsConfirming { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmApplyCommand))]
    public partial bool IsApplying { get; private set; }

    [ObservableProperty]
    public partial string ApplyMessage { get; private set; } = "";

    [ObservableProperty]
    public partial bool ApplyFailed { get; private set; }

    public bool HasProblems => Problems.Length > 0;
    public bool HasApplyMessage => ApplyMessage.Length > 0;
    public bool CanWriteToGame => _applyToGame is not null;

    /// <summary>Ids das funções do anão.</summary>
    public IReadOnlyList<string> RoleIds => _plan.RolesOf(_dwarf);

    internal void SetLevel(string skill, int level) => _plan.Set(_dwarf, skill, Math.Clamp(level, 0, EmbarkPlanRules.MaxLevel));

    internal void RemoveRole(string role) => _plan.RemoveRole(_dwarf, role);

    private bool CanAdd() => NewSkill is not null;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (NewSkill is null) return;
        if (!_plan.For(_dwarf).ContainsKey(NewSkill.Id)) SetLevel(NewSkill.Id, 1);
        NewSkill = null;
    }

    private bool CanAddRole() => NewRole is not null;

    [RelayCommand(CanExecute = nameof(CanAddRole))]
    private void AddRole()
    {
        if (NewRole is null) return;
        _plan.AddRole(_dwarf, NewRole.Id);
        NewRole = null;
    }

    /// <summary>Funções sugeridas pelo app (pela análise de atributos) para este anão.</summary>
    [RelayCommand]
    private void SuggestRoles() =>
        _plan.SetRoles(_dwarf, _analysis.AssignmentsFor(_dwarf).Select(a => a.Role.Id).ToList(), PlanSource.AppSuggestion);

    /// <summary>Limpa os pontos (as funções ficam).</summary>
    [RelayCommand]
    private void Clear() => _plan.ClearPoints(_dwarf);

    private bool CanApply() => CanWriteToGame && !IsConfirming && !IsInSyncWithGame;

    /// <summary>Mostra o que vai mudar no jogo e pede confirmação.</summary>
    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        var levels = _plan.For(_dwarf);
        var problems = EmbarkPlanRules.Check(_analysis.Snapshot, _dwarf, levels);
        if (problems.Count > 0)
        {
            ApplyFailed = true;
            ApplyMessage = "Corrija o plano antes de aplicar: " + string.Join(" ", problems.Select(StripName));
            OnPropertyChanged(nameof(HasApplyMessage));
            return;
        }
        PendingChanges = "Vai mudar no jogo: " + string.Join(", ", EmbarkPlanRules.Diff(_analysis.Snapshot, _dwarf, levels)) + ".";
        ApplyMessage = "";
        OnPropertyChanged(nameof(HasApplyMessage));
        IsConfirming = true;
    }

    private bool CanConfirmApply() => IsConfirming && !IsApplying;

    [RelayCommand(CanExecute = nameof(CanConfirmApply))]
    private async Task ConfirmApplyAsync()
    {
        if (_applyToGame is null) return;
        IsApplying = true;
        try
        {
            var error = await _applyToGame([_dwarf]);
            ApplyFailed = error is not null;
            ApplyMessage = error ?? $"Aplicado no jogo às {DateTime.Now:HH:mm:ss}.";
        }
        finally
        {
            IsApplying = false;
            IsConfirming = false;
            OnPropertyChanged(nameof(HasApplyMessage));
        }
    }

    [RelayCommand]
    private void CancelApply() => IsConfirming = false;

    private void OnPlanChanged(EmbarkDwarf dwarf)
    {
        if (dwarf.Index == _dwarf.Index) Reload();
    }

    /// <summary>Sincroniza com o plano. Só recria as linhas se mudou o conjunto de habilidades (mantém o foco nos botões).</summary>
    private void Reload()
    {
        var s = _analysis.Snapshot;
        var levels = _plan.For(_dwarf);
        var ordered = levels.OrderByDescending(kv => kv.Value).ThenBy(kv => DfNames.Skill(kv.Key, s.SkillCaptions)).ToList();

        if (Rows.Select(r => r.SkillId).ToHashSet().SetEquals(levels.Keys))
        {
            foreach (var row in Rows) row.Level = levels[row.SkillId];
        }
        else
        {
            Rows.Clear();
            foreach (var (skill, level) in ordered)
                Rows.Add(new PlanRowViewModel(this, skill, DfNames.Skill(skill, s.SkillCaptions), level));
        }

        var roleIds = _plan.RolesOf(_dwarf);
        if (!Roles.Select(r => r.Id).SequenceEqual(roleIds))
        {
            Roles.Clear();
            foreach (var id in roleIds)
                Roles.Add(new RoleChipViewModel(this, id, _analysis.Roles.FirstOrDefault(r => r.Id == id)?.Name ?? id));
            RolesChanged?.Invoke();
        }
        RolesText = string.Join(" · ", Roles.Select(r => r.Name));
        HasRoles = Roles.Count > 0;

        var cost = levels.Values.Sum(EmbarkPlanRules.LevelCost);
        var budget = EmbarkPlanRules.Budget(_dwarf);
        BudgetText = $"{cost} de {budget} pontos";
        IsOverBudget = cost > budget;
        Problems = string.Join("\n", EmbarkPlanRules.Check(s, _dwarf, levels).Select(StripName));
        HasRows = Rows.Count > 0;
        OnPropertyChanged(nameof(HasProblems));

        var origin = _plan.OriginOf(_dwarf);
        OriginText = origin?.Describe() ?? "";
        HasOrigin = origin is not null;
        IsFromAi = origin?.Source == PlanSource.Ai;

        var diff = EmbarkPlanRules.Diff(s, _dwarf, levels);
        IsInSyncWithGame = diff.Count == 0;
        GameStatusText = diff.Count == 0
            ? (levels.Count == 0 ? "" : "Igual ao jogo ✓")
            : $"{diff.Count} {(diff.Count == 1 ? "mudança ainda não aplicada" : "mudanças ainda não aplicadas")} no jogo";
        if (IsConfirming && diff.Count == 0) IsConfirming = false;
    }

    // As mensagens das regras começam com "Nome:"; na ficha o nome já está no título.
    private static string StripName(string problem) => problem[(problem.IndexOf(':') + 1)..].Trim();

    public void Dispose() => _plan.Changed -= OnPlanChanged;
}
