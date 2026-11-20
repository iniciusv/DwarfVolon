using DwarfVolon.Core.DwarfFortress;
using DwarfVolon.Core.WorldGen;
using DwarfVolon.ViewModels;

namespace DwarfVolon.Tests;

public sealed class WorldGenEditorViewModelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("dwarfvolon-vm").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private DwarfFortressInstallation FakeInstall()
    {
        var install = Path.Combine(_root, "Dwarf Fortress");
        Directory.CreateDirectory(Path.Combine(install, "data", "init"));
        File.WriteAllText(Path.Combine(install, DwarfFortressInstallation.WindowsExecutable), "");
        return new DwarfFortressInstallation(install, Path.Combine(_root, "appdata"));
    }

    private static TokenRowViewModel Row(WorldGenEditorViewModel vm, string label) =>
        vm.Groups.SelectMany(g => g.Rows).Single(r => r.TokenLabel == label);

    private static void Pick(WorldGenEditorViewModel vm, string category, string option)
    {
        var cat = vm.Categories.Single(c => c.Id == category);
        cat.SelectedOption = cat.Options.Single(o => o.Option.Id == option);
    }

    [Fact]
    public void Inicia_ComRegiaoMedia_TudoPadrao()
    {
        var vm = new WorldGenEditorViewModel();

        Assert.Equal(BuiltInTemplates.DefaultTitle, vm.SelectedBase!.Title);
        Assert.True(vm.IsCalibratedBase);
        Assert.All(vm.Categories, c => Assert.False(c.IsModified));
        Assert.True(vm.HasNoChanges);
        Assert.Equal("MEDIUM REGION PADRAO", vm.NewTitle);
        Assert.True(vm.Profiles.Single(p => p.Profile.Id == "padrao").IsActive);
        Assert.DoesNotContain(vm.Groups.SelectMany(g => g.Rows), r => r.IsChanged);
        Assert.False(vm.SaveCommand.CanExecute(null)); // sem instalação configurada
    }

    [Fact]
    public void EscolherNivel_AtualizaParametrosResumoTituloEMarkdown()
    {
        var vm = new WorldGenEditorViewModel();
        Pick(vm, "rios", "mais");
        Pick(vm, "vulcanismo", "mais");

        Assert.Equal(["300", "300"], Row(vm, "RIVER_MINS").Args.Select(a => a.Value));
        Assert.Equal("9", Row(vm, "VOLCANO_MIN").Args[0].Value);
        Assert.Equal(RiskLevel.Low, vm.Risk);
        Assert.Equal("MEDIUM REGION MAIS RIOS MAIS VULCOES", vm.NewTitle);
        Assert.Equal(["Rios", "Vulcanismo"], vm.SummaryLines.Select(l => l.Category));
        Assert.True(vm.HasWarnings);
        Assert.True(vm.Profiles.Single(p => p.Profile.Id == "rios-vulcoes").IsActive);
        Assert.Contains("|Rios|**Mais rios**|", vm.MarkdownSummary);
        Assert.Contains("[RIVER_MINS:300:300]", vm.PreviewText);
        Assert.Contains("## Risco de rejeição (estimativa): baixo", vm.MarkdownSummary);
    }

    [Fact]
    public void Perfil_AplicaVariasCategorias_EResetVolta()
    {
        var vm = new WorldGenEditorViewModel();
        vm.Profiles.Single(p => p.Profile.Id == "fortaleza-segura").ApplyCommand.Execute(null);

        Assert.Equal("fechadas", vm.Categories.Single(c => c.Id == "cavernas").SelectedOption.Option.Id);
        Assert.Equal("20", Row(vm, "CAVERN_LAYER_OPENNESS_MAX").Args[0].Value);
        Assert.Equal("2", Row(vm, "CAVERN_LAYER_COUNT").Args[0].Value);

        vm.ResetAllCommand.Execute(null);
        Assert.All(vm.Categories, c => Assert.False(c.IsModified));
        Assert.Equal("100", Row(vm, "CAVERN_LAYER_OPENNESS_MAX").Args[0].Value);
    }

    [Fact]
    public void AjusteManual_SobreviveATrocaDeNivel()
    {
        var vm = new WorldGenEditorViewModel();
        Row(vm, "MINERAL_SCARCITY").Args[0].Value = "1234";
        Pick(vm, "rios", "muitos");

        Assert.Equal("1234", Row(vm, "MINERAL_SCARCITY").Args[0].Value);
        Assert.True(vm.HasManualEdits);
        Assert.Contains("[MINERAL_SCARCITY:1234]", vm.PreviewText);

        vm.ClearManualEditsCommand.Execute(null);
        Assert.Equal("2500", Row(vm, "MINERAL_SCARCITY").Args[0].Value);
    }

    [Fact]
    public void AjusteManualArriscado_SobeORisco()
    {
        var vm = new WorldGenEditorViewModel();
        Assert.Equal(RiskLevel.Low, vm.Risk);
        Row(vm, "VOLCANISM").Args[1].Value = "90"; // vulcanismo máximo < 100 com 3 vulcões exigidos
        Assert.Equal(RiskLevel.High, vm.Risk);
        Assert.Contains("Vulcões", vm.RiskDetails);
    }

    [Fact]
    public void ValorInvalido_NaoEntraNoTexto()
    {
        var vm = new WorldGenEditorViewModel();
        var arg = Row(vm, "END_YEAR").Args[0];
        arg.Value = "abc";

        Assert.True(arg.HasError);
        Assert.Contains("[END_YEAR:1050]", vm.PreviewText);
    }

    [Fact]
    public void Catalogo_EmMarkdown_TemTodasAsCategorias()
    {
        var vm = new WorldGenEditorViewModel();
        Assert.All(vm.Categories, c => Assert.Contains($"(`{c.Id}`)", vm.MarkdownCatalog));
    }

    [Fact]
    public void Salvar_GravaNoWorldGenDaInstalacao_EApareceComoBase()
    {
        var install = FakeInstall();
        var vm = new WorldGenEditorViewModel { Installation = install };
        Pick(vm, "rios", "mais");
        vm.NewTitle = "Meu mundo de rios";

        vm.SaveCommand.Execute(null);

        var saved = WorldGenDocument.Load(install.WorldGenFilePath).FindByTitle("MEU MUNDO DE RIOS");
        Assert.NotNull(saved);
        Assert.Equal(["300", "300"], saved.Find("RIVER_MINS")!.Values);
        Assert.Contains(vm.BaseOptions, o => o.Source == BaseOptionViewModel.FromUserFile && o.Title == "MEU MUNDO DE RIOS");
        Assert.True(vm.HasTitleWarning); // agora o título já existe no arquivo

        vm.SaveCommand.Execute(null); // de novo: substitui e faz backup
        Assert.Single(WorldGenDocument.Load(install.WorldGenFilePath).ParameterSets);
        Assert.Single(Directory.GetFiles(Path.Combine(install.PrefsDirectory, WorldGenRepository.BackupFolderName)));
    }
}
