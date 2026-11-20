using DwarfVolon.Core.Embark;
using DwarfVolon.DFHack;
using DwarfVolon.ViewModels;

namespace DwarfVolon.Tests;

public sealed class EmbarkViewModelTests
{
    private static EmbarkSnapshot Snapshot() => new()
    {
        State = EmbarkScreenState.Embark,
        PointsRemaining = 1000,
        EmbarkPoints = 2000,
        Dwarves =
        [
            new EmbarkDwarf { Index = 0, Name = "Urist", PicksLeft = 10, Physical = new Dictionary<string, int> { ["STRENGTH"] = 1500 } },
            new EmbarkDwarf { Index = 1, Name = "Bomrek", PicksLeft = 10, Mental = new Dictionary<string, int> { ["ANALYTICAL_ABILITY"] = 1500 } },
        ],
    };

    [Fact]
    public async Task Refresh_on_embark_screen_builds_analysis()
    {
        var vm = new EmbarkViewModel(_ => Task.FromResult(Snapshot()), () => "DFHack teste");
        await vm.RefreshAsync();

        Assert.False(vm.HasError);
        Assert.NotNull(vm.Analysis);
        Assert.Contains("Sugestão de funções", vm.Markdown);
        Assert.Contains("Urist", vm.Markdown);
        Assert.StartsWith("DFHack teste · 2 anões", vm.StatusText);
    }

    [Fact]
    public async Task Refresh_on_other_screen_explains_how_to_get_there()
    {
        var vm = new EmbarkViewModel(_ => Task.FromResult(new EmbarkSnapshot { State = EmbarkScreenState.OtherScreen, Screen = "title" }));
        await vm.RefreshAsync();

        Assert.False(vm.HasError);
        Assert.Null(vm.Analysis);
        Assert.Contains("Preparar com cuidado", vm.Markdown);
        Assert.Contains("`title`", vm.Markdown);
    }

    [Fact]
    public async Task Refresh_without_dfhack_shows_error()
    {
        var vm = new EmbarkViewModel(_ => throw new DfHackException("sem conexão"));
        await vm.RefreshAsync();

        Assert.True(vm.HasError);
        Assert.Equal("sem conexão", vm.StatusText);
        Assert.Contains("Não consegui falar com o **DFHack**", vm.Markdown);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Script_error_keeps_last_table()
    {
        var calls = 0;
        var vm = new EmbarkViewModel(_ => ++calls == 1 ? Task.FromResult(Snapshot()) : throw new EmbarkReadException("erro no script"));
        await vm.RefreshAsync();
        var before = vm.Markdown;
        await vm.RefreshAsync();

        Assert.True(vm.HasError);
        Assert.Equal("erro no script", vm.StatusText);
        Assert.Equal(before, vm.Markdown);
    }
}
