using DwarfVolon.Core.Markdown;
using DwarfVolon.Core.WorldGen;
using Xunit.Abstractions;

namespace DwarfVolon.Core.Tests;

public class RiskEstimatorTests(ITestOutputHelper output)
{
    private static readonly IReadOnlyList<ModifierCategory> Categories = ModifierLibrary.LoadBuiltInCategories();

    private static WorldGenParameterSet Medium() => BuiltInTemplates.Load().FindByTitle(BuiltInTemplates.DefaultTitle)!.Clone();

    private static WorldGenParameterSet With(params (string Category, string Option)[] picks)
    {
        var set = Medium();
        PresetApplier.Apply(set, picks.Select(p =>
        {
            var c = Categories.Single(x => x.Id == p.Category);
            return c.FindOption(p.Option)!.ToPreset(c);
        }));
        return set;
    }

    [Fact]
    public void PadraoDoJogo_RiscoBaixoEmTudo()
    {
        var items = WorldGenRiskEstimator.Estimate(Medium());
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.True(i.Level == RiskLevel.Low, $"{i.Label}: {i.Detail}"));
    }

    [Fact]
    public void RiosEmQualquerNivel_NaoMudaORisco()
    {
        foreach (var option in Categories.Single(c => c.Id == "rios").Options)
            Assert.Equal(RiskLevel.Low, WorldGenRiskEstimator.Overall(WorldGenRiskEstimator.Estimate(With(("rios", option.Id)))));
    }

    [Fact]
    public void TodosOsNiveis_NaRegiaoMedia_SemRiscoAlto()
    {
        var rows = new List<IReadOnlyList<object?>>();
        foreach (var category in Categories)
        foreach (var option in category.Options)
        {
            var items = WorldGenRiskEstimator.Estimate(With((category.Id, option.Id)));
            var overall = WorldGenRiskEstimator.Overall(items);
            rows.Add([category.Name, option.Name, WorldGenRiskEstimator.Describe(overall),
                string.Join("; ", items.Where(i => i.Level > RiskLevel.Low).Select(i => $"{i.Label}: {i.Detail}"))]);
            Assert.True(overall < RiskLevel.High, $"{category.Id}/{option.Id}: {string.Join(" | ", items.Where(i => i.Level == RiskLevel.High))}");
        }
        output.WriteLine(MarkdownTable.Render(["Categoria", "Nível", "Risco", "Pontos de atenção"], rows));
    }

    [Fact]
    public void Perfis_SemRiscoAlto()
    {
        foreach (var profile in ModifierLibrary.LoadBuiltInProfiles())
        {
            var set = With(profile.Selections.Select(s => (s.Key, s.Value)).ToArray());
            var items = WorldGenRiskEstimator.Estimate(set);
            output.WriteLine($"{profile.Name}: {WorldGenRiskEstimator.Describe(WorldGenRiskEstimator.Overall(items))}");
            Assert.True(WorldGenRiskEstimator.Overall(items) < RiskLevel.High, profile.Id);
        }
    }

    [Fact]
    public void PiorCombinacaoPossivel_SemRiscoAlto()
    {
        // O nível mais "extremo" de cada categoria ao mesmo tempo.
        var set = With(("rios", "muitos"), ("vulcanismo", "muito"), ("relevo", "montanhoso"), ("perigo", "brutal"),
            ("cavernas", "fechadas"), ("camadas", "uma"), ("minerios", "muito"));
        Assert.True(WorldGenRiskEstimator.Overall(WorldGenRiskEstimator.Estimate(set)) < RiskLevel.High);

        set = With(("rios", "muitos"), ("vulcanismo", "muito"), ("relevo", "plano"), ("perigo", "brutal"));
        Assert.True(WorldGenRiskEstimator.Overall(WorldGenRiskEstimator.Estimate(set)) < RiskLevel.High);
    }

    [Fact]
    public void CadaToken_EhAlteradoPorUmaCategoriaSo()
    {
        var owners = Categories
            .SelectMany(c => c.Options.SelectMany(o => o.Operations).Select(op => (Token: op.Key is null ? op.Token : $"{op.Token}:{op.Key}", c.Id)))
            .Distinct()
            .GroupBy(x => x.Token)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Id))}")
            .ToList();
        Assert.Empty(owners);
    }

    [Fact]
    public void DetectaConfiguracoesErradas()
    {
        // Antigo "Brutal": exigia 2,5x os quadrados de selvageria alta sem mudar a distribuição.
        var savage = Medium();
        savage.Set("SAVAGERY_RANGES", "832", "4160", "5200");
        Assert.Contains(WorldGenRiskEstimator.Estimate(savage), i => i.Label == "Selvageria alto" && i.Level >= RiskLevel.Medium);

        // Vulcões exigidos com vulcanismo máximo abaixo de 100.
        var volcano = Medium();
        volcano.Set("VOLCANISM", "0", "90", "200", "200");
        Assert.Contains(WorldGenRiskEstimator.Estimate(volcano), i => i.Label == "Vulcões" && i.Level == RiskLevel.High);

        // Mudar a distribuição sem baixar o mínimo do meio.
        var shifted = Medium();
        shifted.Set("VOLCANISM_FREQUENCY", "1", "1", "1", "1", "1", "6");
        Assert.Contains(WorldGenRiskEstimator.Estimate(shifted), i => i.Label == "Vulcanismo médio" && i.Level == RiskLevel.High);
    }
}
