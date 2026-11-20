using System.Text.Json;
using DwarfVolon.Core.WorldGen;

namespace DwarfVolon.Core.Tests;

public class ModifierTests
{
    private static WorldGenParameterSet Medium() => BuiltInTemplates.Load().FindByTitle(BuiltInTemplates.DefaultTitle)!.Clone();

    private static readonly IReadOnlyList<ModifierCategory> Categories = ModifierLibrary.LoadBuiltInCategories();

    private static ModifierOption Option(string category, string option) =>
        Categories.Single(c => c.Id == category).FindOption(option)!;

    private static WorldGenParameterSet ApplyToMedium(params (string Category, string Option)[] picks)
    {
        var target = Medium();
        var presets = picks.Select(p => Option(p.Category, p.Option).ToPreset(Categories.Single(c => c.Id == p.Category)));
        var result = PresetApplier.Apply(target, presets);
        Assert.Empty(result.Warnings);
        return target;
    }

    [Fact]
    public void Categorias_CarregamEmOrdem_ComUmPadraoCada()
    {
        Assert.Equal(["rios", "vulcanismo", "relevo", "minerios", "perigo", "cavernas", "camadas", "historia", "embarque"],
            Categories.Select(c => c.Id));
        Assert.All(Categories, c =>
        {
            Assert.Single(c.Options, o => o.IsDefault);
            Assert.Empty(c.DefaultOption.Operations);
            Assert.Equal(c.Options.Count, c.Options.Select(o => o.Id).Distinct().Count());
            Assert.All(c.Options.Where(o => !o.IsDefault), o => Assert.NotEmpty(o.Operations));
        });
    }

    [Fact]
    public void TodasAsOpcoes_AplicamSemAvisos_EmTodasAsBases()
    {
        foreach (var baseSet in BuiltInTemplates.Load().ParameterSets)
        foreach (var category in Categories)
        foreach (var option in category.Options)
        {
            var result = PresetApplier.Apply(baseSet.Clone(), option.ToPreset(category));
            Assert.True(result.Warnings.Count == 0, $"{category.Id}/{option.Id} em {baseSet.Title}: {string.Join(" | ", result.Warnings)}");
        }
    }

    [Fact]
    public void Perfis_ReferenciamCategoriasEOpcoesExistentes()
    {
        var profiles = ModifierLibrary.LoadBuiltInProfiles();
        Assert.Contains(profiles, p => p.Id == "rios-vulcoes");
        Assert.All(profiles, p => Assert.All(p.Selections, s =>
            Assert.NotNull(Categories.Single(c => c.Id == s.Key).FindOption(s.Value))));
    }

    [Fact]
    public void RiosMaisEVulcoesMais_NoMundoMedio()
    {
        var target = ApplyToMedium(("rios", "mais"), ("vulcanismo", "mais"));

        Assert.Equal(["300", "300"], target.Find("RIVER_MINS")!.Values);
        Assert.Equal(["250"], target.Find("EROSION_CYCLE_COUNT")!.Values); // "Mais rios" não mexe na erosão
        Assert.Equal(["9"], target.Find("VOLCANO_MIN")!.Values);
        Assert.Equal(["0", "100", "200", "200"], target.Find("VOLCANISM")!.Values);
        Assert.Equal(["1", "1", "1", "1", "1", "3"], target.Find("VOLCANISM_FREQUENCY")!.Values);
        Assert.Equal(["1664", "2496", "2080"], target.Find("VOLCANISM_RANGES")!.Values);
    }

    [Fact]
    public void Rios_SoMexemNasNascentesENaErosao()
    {
        var tokens = Categories.Single(c => c.Id == "rios").Options.SelectMany(o => o.Operations).Select(o => o.Token).Distinct();
        Assert.Equal(["RIVER_MINS", "EROSION_CYCLE_COUNT"], tokens);
    }

    [Fact]
    public void CavernasFechadas_EUmaCamada()
    {
        var target = ApplyToMedium(("cavernas", "fechadas"), ("camadas", "uma"));

        Assert.Equal(["20"], target.Find("CAVERN_LAYER_OPENNESS_MAX")!.Values);
        Assert.Equal(["25"], target.Find("CAVERN_LAYER_PASSAGE_DENSITY_MAX")!.Values);
        Assert.Equal(["1"], target.Find("CAVERN_LAYER_COUNT")!.Values);
    }

    [Fact]
    public void PerigoBrutal_MudaDistribuicaoEmVezDeSoExigirMais()
    {
        var target = ApplyToMedium(("perigo", "brutal"));
        Assert.Equal(["1", "1", "1", "1", "1", "3"], target.Find("SAVAGERY_FREQUENCY")!.Values);
        Assert.Equal(["2080", "2912", "2080"], target.Find("SAVAGERY_RANGES")!.Values);
    }

    [Fact]
    public void CategoriaDoUsuario_SubstituiEmbutidaComMesmoId()
    {
        var dir = Directory.CreateTempSubdirectory("dwarfvolon-mods").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "rios.json"), """
                { "id": "rios", "name": "Meus rios", "order": 10,
                  "options": [ { "id": "padrao", "name": "Padrão", "isDefault": true },
                               { "id": "x", "name": "X", "operations": [ { "token": "RIVER_MINS", "set": [1, 1] } ] } ] }
                """);
            File.WriteAllText(Path.Combine(dir, "nova.json"), """{ "id": "nova", "name": "Nova", "order": 999, "options": [ { "id": "a", "name": "A", "isDefault": true } ] }""");
            File.WriteAllText(Path.Combine(dir, "ruim.json"), "{ isso não é json");
            var errors = new List<string>();

            var merged = ModifierLibrary.Merge(Categories, ModifierLibrary.LoadCategoriesFromDirectory(dir, errors));

            Assert.Single(errors);
            Assert.Equal("Meus rios", merged.Single(c => c.Id == "rios").Name);
            Assert.Equal("nova", merged[^1].Id);
            Assert.Equal(Categories.Count + 1, merged.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Operacoes_SetMultiplyAddMinMax()
    {
        var set = WorldGenParameterSet.Create("T");
        set.Set("RIVER_MINS", "10", "10");
        var preset = Preset(new PresetOperation
        {
            Token = "RIVER_MINS",
            Set = [Json("null"), Json("20")],
            Multiply = [3, 3],
            Add = [1, null],
            Min = [null, 100],
            Max = [25, null],
        });

        PresetApplier.Apply(set, preset);

        // 10*3+1 = 31 → max 25 ; 20*3 = 60 → min 100
        Assert.Equal(["25", "100"], set.Find("RIVER_MINS")!.Values);
    }

    [Fact]
    public void Operacao_TokenComChave()
    {
        var set = WorldGenParameterSet.Create("T");
        set.Set("REGION_COUNTS", "MOUNTAINS", "100", "2", "2");
        set.Set("REGION_COUNTS", "OCEAN", "100", "2", "2");

        PresetApplier.Apply(set, Preset(new PresetOperation { Token = "REGION_COUNTS", Key = "MOUNTAINS", Multiply = [2, null, null] }));

        Assert.Equal(["200", "2", "2"], set.Find("REGION_COUNTS", "MOUNTAINS")!.Values);
        Assert.Equal(["100", "2", "2"], set.Find("REGION_COUNTS", "OCEAN")!.Values);
    }

    [Fact]
    public void TokenAusente_SetCompletoInsere_MultiplyAvisa()
    {
        var set = WorldGenParameterSet.Create("T");
        var result = PresetApplier.Apply(set, Preset(
            new PresetOperation { Token = "NIGHTMARE_NUMBER", Set = [Json("5")] },
            new PresetOperation { Token = "VAMPIRE_NUMBER", Multiply = [2] }));

        Assert.Equal(["5"], set.Find("NIGHTMARE_NUMBER")!.Values);
        Assert.Null(set.Find("VAMPIRE_NUMBER"));
        Assert.Single(result.Warnings);
    }

    private static WorldGenPreset Preset(params PresetOperation[] ops) =>
        new() { Id = "t", Name = "Teste", Operations = [.. ops] };

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();
}
