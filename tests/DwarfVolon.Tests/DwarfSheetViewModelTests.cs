using DwarfVolon.Core.Embark;
using DwarfVolon.ViewModels;

namespace DwarfVolon.Tests;

public sealed class DwarfSheetViewModelTests
{
    private static readonly Dictionary<string, IReadOnlyList<int>> Ranges = new()
    {
        ["STRENGTH"] = [450, 950, 1150, 1250, 1350, 1550, 2250],
    };

    private static readonly IReadOnlyList<EmbarkDwarf> Dwarves =
    [
        new EmbarkDwarf
        {
            Index = 0, UnitId = 7, Name = "Besmar", Age = 23, Sex = 0, PicksLeft = 10,
            Physical = new Dictionary<string, int> { ["STRENGTH"] = 1350 },
            Traits = new Dictionary<string, int> { ["HATE_PROPENSITY"] = 81, ["BRAVERY"] = 30, ["HUMOR"] = 50 },
            Beliefs = [new EmbarkBelief("TRANQUILITY", 49)],
            Preferences = [new EmbarkPreference("HateCreature", "hamsters"), new EmbarkPreference("LikeMaterial", "steel")],
        },
        new EmbarkDwarf { Index = 1, UnitId = 8, Name = "Rakust", PicksLeft = 10 },
    ];

    private static EmbarkSnapshot Snapshot(int sheetUnit, string text) => new()
    {
        State = EmbarkScreenState.Embark,
        AttributeRanges = Ranges,
        Dwarves = Dwarves,
        Sheet = new EmbarkOpenSheet(sheetUnit, EmbarkOpenSheet.PersonalitySubTab, 0, [text]),
    };

    [Fact]
    public async Task Refresh_builds_sheets_with_charts()
    {
        var vm = new EmbarkViewModel(_ => Task.FromResult(Snapshot(7, "[C:2:0:0]She is strong.")));
        await vm.RefreshAsync();

        Assert.Equal(2, vm.Dwarves.Count);
        var sheet = vm.Dwarves[0];
        Assert.Equal("Besmar", sheet.Name);
        Assert.Contains("23 anos", sheet.Subtitle);
        Assert.Equal(67, Assert.Single(sheet.PhysicalBars).Value); // 1350 = 5º ponto da faixa (4/6)
        Assert.Equal(["Propensão ao ódio", "Coragem"], sheet.FacetBars.Select(b => b.Label)); // neutro (50) fica de fora
        Assert.Equal("+49", Assert.Single(sheet.BeliefBars).ValueText);
        Assert.Equal("Materiais", sheet.PreferenceGroups[0].Kind); // ódios por último
        Assert.True(sheet.PreferenceGroups[^1].IsHate);
        Assert.DoesNotContain(sheet.RoleBars, r => r.IsHighlighted); // sem funções definidas, nada em destaque
        Assert.False(sheet.Plan.HasRoles);
        Assert.True(sheet.HasSheetText);
        Assert.False(vm.Dwarves[1].HasSheetText);
    }

    [Fact]
    public async Task New_sheet_text_rebuilds_but_keeps_selection_and_old_text()
    {
        var snapshot = Snapshot(7, "She is strong.");
        var vm = new EmbarkViewModel(_ => Task.FromResult(snapshot));
        await vm.RefreshAsync();
        vm.SelectedDwarf = vm.Dwarves[1];

        snapshot = Snapshot(8, "She is calm.");
        await vm.RefreshAsync();

        Assert.Equal("Rakust", vm.SelectedDwarf?.Name);
        Assert.True(vm.SelectedDwarf!.HasSheetText);
        Assert.True(vm.Dwarves[0].HasSheetText); // o texto do primeiro continua guardado
    }
}
