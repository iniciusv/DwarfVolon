using DwarfVolon.Core.Embark;
using DwarfVolon.ViewModels;

namespace DwarfVolon.Tests;

public sealed class EmbarkPlanViewModelTests : IDisposable
{
    private readonly string _plans = Directory.CreateTempSubdirectory("dwarfvolon-plans").FullName;

    public void Dispose() => Directory.Delete(_plans, recursive: true);

    private static EmbarkSnapshot Snapshot(params EmbarkSkill[] besmarChosen) => new()
    {
        State = EmbarkScreenState.Embark,
        SaveDir = "region2",
        World = "The Cyclopean Universe",
        SkillTabs = new Dictionary<string, IReadOnlyList<string>>
        {
            ["CRUCIAL"] = ["MINING", "CARPENTRY"],
            ["OTHER"] = ["DIAGNOSE", "SET_BONE"],
        },
        Dwarves =
        [
            new EmbarkDwarf
            {
                Index = 0, UnitId = 1, HistFigureId = 94850, Name = "Besmar Ustuthkifed",
                PicksLeft = 10 - besmarChosen.Sum(k => k.Level), Skills = besmarChosen,
            },
            new EmbarkDwarf { Index = 1, UnitId = 2, HistFigureId = 94844, Name = "èrith Eribzuden", PicksLeft = 10 },
        ],
    };

    /// <summary>Jogo falso: guarda o que foi aplicado e a próxima leitura devolve isso como escolhido.</summary>
    private sealed class FakeGame
    {
        public EmbarkSnapshot Current { get; set; } = Snapshot();
        public List<EmbarkApplyItem> Applied { get; } = [];

        public Task<EmbarkSnapshot> ReadAsync(CancellationToken _) => Task.FromResult(Current);

        public Task<EmbarkApplyResult> ApplyAsync(IReadOnlyList<EmbarkApplyItem> items, CancellationToken _)
        {
            Applied.AddRange(items);
            if (items.FirstOrDefault(i => i.Index == 0) is { } besmar)
                Current = Snapshot([.. besmar.Skills.Select(kv => new EmbarkSkill(kv.Key, kv.Value))]);
            return Task.FromResult(new EmbarkApplyResult { Ok = true, AppliedCount = items.Count });
        }
    }

    private async Task<(EmbarkViewModel Vm, FakeGame Game)> LoadedAsync(bool persist = false)
    {
        var game = new FakeGame();
        var vm = new EmbarkViewModel(game.ReadAsync, apply: game.ApplyAsync, planDirectory: persist ? _plans : null);
        await vm.RefreshAsync();
        return (vm, game);
    }

    [Fact]
    public async Task Nothing_is_decided_automatically()
    {
        var (vm, _) = await LoadedAsync();

        Assert.All(vm.Dwarves, d =>
        {
            Assert.False(d.Plan.HasRoles);
            Assert.False(d.Plan.HasRows);
            Assert.Equal("", d.Plan.OriginText);
        });
        Assert.DoesNotContain("Decisões até agora", vm.Markdown);
    }

    [Fact]
    public async Task Pasted_ai_response_fills_points_and_roles()
    {
        var (vm, _) = await LoadedAsync();
        vm.SelectedTab = 1;

        var ok = vm.ImportAiResponse(
            "Aqui:\n```dwarfvolon-pontos\nBesmar Ustuthkifed: SET_BONE 5, CARPENTRY 1 | funções: Médico, Carpinteiro\n" +
            "Erith Eribzuden: DIAGNOSE 5, MINING 1 | funções: minerador\n```");

        Assert.True(ok);
        Assert.Equal(0, vm.SelectedTab); // volta para as fichas
        var editor = vm.Dwarves[0].Plan;
        Assert.Equal(["SET_BONE", "CARPENTRY"], editor.Rows.Select(r => r.SkillId));
        Assert.Equal(["medico", "carpinteiro"], editor.Roles.Select(r => r.Id));
        Assert.Equal("Médico · Carpinteiro", editor.RolesText);
        Assert.Equal("6 de 10 pontos", editor.BudgetText);
        Assert.True(editor.IsFromAi);
        Assert.Contains(vm.Dwarves[0].RoleBars, b => b.Label == "Médico" && b.IsHighlighted);
        Assert.Contains("## Decisões até agora", vm.Markdown); // o resumo para a IA leva o que já foi decidido

        editor.Rows.First(r => r.SkillId == "CARPENTRY").IncreaseCommand.Execute(null);
        Assert.EndsWith("com ajustes seus.", editor.OriginText);
    }

    [Fact]
    public async Task Suggest_buttons_fill_roles_only()
    {
        var (vm, _) = await LoadedAsync();

        vm.Dwarves[0].Plan.SuggestRolesCommand.Execute(null);
        Assert.True(vm.Dwarves[0].Plan.HasRoles);
        Assert.False(vm.Dwarves[0].Plan.HasRows); // pontos não
        Assert.Equal("Funções sugeridas pelo app.", vm.Dwarves[0].Plan.OriginText);
        Assert.False(vm.Dwarves[1].Plan.HasRoles);

        vm.SuggestRolesForAllCommand.Execute(null);
        Assert.All(vm.Dwarves, d => Assert.True(d.Plan.HasRoles));
    }

    [Fact]
    public async Task Roles_can_be_added_and_removed_by_hand()
    {
        var (vm, _) = await LoadedAsync();
        var editor = vm.Dwarves[1].Plan;

        editor.NewRole = editor.AvailableRoles.Single(r => r.Id == "minerador");
        editor.AddRoleCommand.Execute(null);
        editor.NewRole = editor.AvailableRoles.Single(r => r.Id == "medico");
        editor.AddRoleCommand.Execute(null);
        Assert.Equal("Minerador · Médico", editor.RolesText);
        Assert.Equal("Montado à mão.", editor.OriginText);

        editor.Roles[0].RemoveCommand.Execute(null);
        Assert.Equal("Médico", editor.RolesText);
    }

    [Fact]
    public async Task Apply_shows_changes_then_writes_and_rereads()
    {
        var (vm, game) = await LoadedAsync();
        var editor = vm.Dwarves[0].Plan;
        editor.NewSkill = editor.AvailableSkills.Single(o => o.Id == "MINING");
        editor.AddCommand.Execute(null);
        Assert.Equal("1 mudança ainda não aplicada no jogo", editor.GameStatusText);

        editor.ApplyCommand.Execute(null);
        Assert.True(editor.IsConfirming);
        Assert.Contains("Mineração 0 → 1", editor.PendingChanges);
        Assert.Empty(game.Applied); // nada escrito antes de confirmar

        await editor.ConfirmApplyCommand.ExecuteAsync(null);
        var applied = Assert.Single(game.Applied);
        Assert.Equal(1, applied.UnitId);
        Assert.Equal(1, applied.Skills["MINING"]);

        // Releu o jogo: a ficha nova mostra que está igual, e o plano continua lá.
        var fresh = vm.Dwarves[0].Plan;
        Assert.Equal("Igual ao jogo ✓", fresh.GameStatusText);
        Assert.False(fresh.ApplyCommand.CanExecute(null));
        Assert.Equal(1, fresh.Rows.Single().Level);
    }

    [Fact]
    public async Task Apply_refuses_plan_over_budget()
    {
        var (vm, game) = await LoadedAsync();
        vm.ImportAiResponse("```dwarfvolon-pontos\nBesmar Ustuthkifed: SET_BONE 5, CARPENTRY 5, MINING 5\n```");
        var editor = vm.Dwarves[0].Plan;

        editor.ApplyCommand.Execute(null);
        Assert.False(editor.IsConfirming);
        Assert.True(editor.ApplyFailed);
        Assert.Contains("15 pontos", editor.ApplyMessage);
        Assert.Empty(game.Applied);
    }

    [Fact]
    public async Task Apply_all_confirms_and_writes_every_changed_dwarf()
    {
        var (vm, game) = await LoadedAsync();
        vm.ImportAiResponse("```dwarfvolon-pontos\nBesmar Ustuthkifed: SET_BONE 5\nèrith Eribzuden: DIAGNOSE 5\n```");

        vm.ApplyAllCommand.Execute(null);
        Assert.True(vm.IsConfirmingApplyAll);
        Assert.Contains("Besmar Ustuthkifed: Imobilizar ossos 0 → 5", vm.PendingApplyAll);
        Assert.Empty(game.Applied);

        await vm.ConfirmApplyAllCommand.ExecuteAsync(null);
        Assert.Equal(2, game.Applied.Count);
        Assert.False(vm.ApplyAllFailed);
        Assert.False(vm.IsConfirmingApplyAll);
    }

    [Fact]
    public async Task Decisions_are_saved_per_world_and_restored()
    {
        var (vm, _) = await LoadedAsync(persist: true);
        vm.ImportAiResponse("```dwarfvolon-pontos\nèrith Eribzuden: DIAGNOSE 5, MINING 1 | funções: Médico, Minerador\n```");

        var file = Path.Combine(_plans, "region2.json");
        Assert.True(File.Exists(file));
        Assert.Contains("Decisões salvas", vm.PlanFileText);
        var saved = EmbarkPlanStore.Read(file)!;
        var entry = Assert.Single(saved.Dwarves);
        Assert.Equal(94844, entry.HistFigureId);
        Assert.Equal(["medico", "minerador"], entry.Roles);

        // App reaberto: as decisões voltam.
        var (again, _) = await LoadedAsync(persist: true);
        Assert.Contains("restauradas", again.PlanFileText);
        Assert.Equal("Médico · Minerador", again.Dwarves[1].Plan.RolesText);
        Assert.Equal(5, again.Dwarves[1].Plan.Rows.Single(r => r.SkillId == "DIAGNOSE").Level);
        Assert.True(again.Dwarves[1].Plan.IsFromAi);
    }

    [Fact]
    public async Task Request_is_separate_from_summary_unless_asked()
    {
        var (vm, _) = await LoadedAsync();
        vm.AiInstructions = "Só 1 ponto em mineração.";

        var request = vm.PointsRequestForAi()!;
        Assert.Contains("Só 1 ponto em mineração.", request);
        Assert.Contains("| funções:", request);
        Assert.DoesNotContain("## Nota por função", request);

        vm.IncludeSummaryInRequest = true;
        Assert.Contains("## Nota por função", vm.PointsRequestForAi());
    }

    [Fact]
    public async Task Bad_response_reports_and_keeps_plan()
    {
        var (vm, _) = await LoadedAsync();
        vm.ImportAiResponse("```dwarfvolon-pontos\nBesmar Ustuthkifed: MINING 2\n```");

        Assert.False(vm.ImportAiResponse("Não sei, me conte mais sobre a fortaleza."));
        Assert.True(vm.ImportFailed);
        Assert.Contains("dwarfvolon-pontos", vm.ImportProblems);
        Assert.Equal(2, vm.Dwarves[0].Plan.Rows.Single().Level);
        Assert.False(vm.ImportAiResponse("   "));
    }

    [Fact]
    public async Task Editor_buttons_change_levels_within_rules()
    {
        var (vm, _) = await LoadedAsync();
        var editor = vm.Dwarves[0].Plan;

        editor.NewSkill = editor.AvailableSkills.Single(o => o.Id == "MINING");
        editor.AddCommand.Execute(null);
        var row = Assert.Single(editor.Rows);
        Assert.False(row.DecreaseCommand.CanExecute(null)); // mínimo 1; para tirar, use ✕
        for (var i = 0; i < 6; i++) if (row.IncreaseCommand.CanExecute(null)) row.IncreaseCommand.Execute(null);
        Assert.Equal(5, row.Level);
        Assert.Same(row, editor.Rows.Single()); // a mesma linha: não recria a cada clique

        foreach (var id in new[] { "SET_BONE", "CARPENTRY" })
        {
            editor.NewSkill = editor.AvailableSkills.Single(o => o.Id == id);
            editor.AddCommand.Execute(null);
        }
        for (var i = 0; i < 4; i++) editor.Rows.Single(r => r.SkillId == "SET_BONE").IncreaseCommand.Execute(null);
        Assert.Equal("11 de 10 pontos", editor.BudgetText);
        Assert.True(editor.IsOverBudget);

        editor.ClearCommand.Execute(null);
        Assert.False(editor.HasRows);
    }
}
