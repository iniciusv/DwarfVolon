using DwarfVolon.Core.Embark;
using DwarfVolon.Core.Markdown;

namespace DwarfVolon.Core.Tests;

public sealed class EmbarkTests
{
    // Faixa de Força de anão no DF 53 (mediana 1250).
    private static readonly int[] StrengthRange = [450, 950, 1150, 1250, 1350, 1550, 2250];

    /// <summary>JSON no formato do embark.lua, incluindo "[]" para dicionários vazios (como o Lua escreve).</summary>
    internal static string SampleJson(int dwarves = 3) =>
        "aviso qualquer do console\n" +
        "{\"ok\":true,\"status\":\"embark\",\"focus\":\"setupdwarfgame/Dwarves\",\"points_remaining\":1400,\"embark_points\":2256," +
        "\"cur_year\":250,\"skill_tabs\":{\"CRUCIAL\":[\"MINING\"],\"OTHER\":[]}," +
        "\"skill_captions\":{\"MINING\":\"Mining\",\"WAX_WORKING\":\"Wax Working\"}," +
        "\"ranges\":{\"physical\":{\"STRENGTH\":[450,950,1150,1250,1350,1550,2250]},\"mental\":[]}," +
        "\"dwarves\":[" + string.Join(',', Enumerable.Range(0, dwarves).Select(Dwarf)) + "]}\n";

    private static string Dwarf(int i) =>
        $"{{\"index\":{i},\"name\":\"Urist {i}\",\"name_english\":\"Urist {i}\",\"sex\":{i % 2},\"birth_year\":{200 + i}," +
        $"\"picks_left\":{(i == 0 ? 8 : 10)},\"profession\":\"STANDARD\"," +
        $"\"skills\":{(i == 0 ? "[{\"id\":\"MINING\",\"level\":2}]" : "[]")}," +
        $"\"physical\":{{\"STRENGTH\":{1000 + 200 * i},\"AGILITY\":{900 - 50 * i}}}," +
        $"\"mental\":{{\"ANALYTICAL_ABILITY\":{1500 - 200 * i}}}," +
        $"\"traits\":{(i == 1 ? "{\"STRESS_VULNERABILITY\":90,\"BRAVERY\":10}" : "[]")}}}";

    [Fact]
    public void Parse_reads_embark_screen()
    {
        var s = EmbarkJson.Parse(SampleJson());

        Assert.Equal(EmbarkScreenState.Embark, s.State);
        Assert.Equal(1400, s.PointsRemaining);
        Assert.Equal(2256, s.EmbarkPoints);
        Assert.Equal(3, s.Dwarves.Count);
        Assert.Equal(["MINING"], s.SkillTabs["CRUCIAL"]);
        Assert.Equal(StrengthRange, s.AttributeRanges["STRENGTH"]);

        var first = s.Dwarves[0];
        Assert.Equal("Urist 0", first.Name);
        Assert.Equal(2, first.SkillLevel("MINING"));
        Assert.Equal(8, first.PicksLeft);
        Assert.Equal(1000, first.Attribute("STRENGTH"));
        Assert.Equal(1500, first.Attribute("ANALYTICAL_ABILITY"));
        Assert.Empty(first.Traits);
        Assert.Equal(50, s.AgeOf(first));
    }

    [Fact]
    public void Parse_other_screen()
    {
        var s = EmbarkJson.Parse("{\"ok\":true,\"status\":\"other\",\"screen\":\"title\"}");
        Assert.Equal(EmbarkScreenState.OtherScreen, s.State);
        Assert.Equal("title", s.Screen);
    }

    [Fact]
    public void Parse_script_error_and_garbage_throw()
    {
        var ex = Assert.Throws<EmbarkReadException>(() => EmbarkJson.Parse("{\"ok\":false,\"error\":\"boom\"}"));
        Assert.Contains("boom", ex.Message);
        Assert.Throws<EmbarkReadException>(() => EmbarkJson.Parse("(lua command):1: attempt to index a nil value"));
    }

    [Theory]
    [InlineData(450, 0)]
    [InlineData(100, 0)]
    [InlineData(1250, 50)]
    [InlineData(2250, 100)]
    [InlineData(3000, 100)]
    [InlineData(1200, 42)] // metade entre o 3º (1150 → 33) e o 4º ponto (1250 → 50)
    public void ScoreAttribute_interpolates_caste_range(int value, int expected) =>
        Assert.Equal(expected, EmbarkAnalyzer.ScoreAttribute(value, StrengthRange));

    [Fact]
    public void ScoreAttribute_without_range_uses_typical_value() =>
        Assert.Equal(50, EmbarkAnalyzer.ScoreAttribute(1000, null));

    [Theory]
    [InlineData(10, 2, new[] { 5, 5 })]
    [InlineData(7, 2, new[] { 5, 2 })]
    [InlineData(3, 2, new[] { 2, 1 })]
    [InlineData(1, 2, new[] { 1, 0 })]
    [InlineData(0, 2, new[] { 0, 0 })]
    [InlineData(20, 1, new[] { 5 })]
    public void DistributeLevels_gives_one_each_then_fills_primary(int budget, int roles, int[] expected) =>
        Assert.Equal(expected, EmbarkAnalyzer.DistributeLevels(budget, roles));

    [Fact]
    public void Hungarian_finds_optimal_not_greedy()
    {
        // Guloso daria a linha 0 para a coluna 0 (custo 1) e a linha 1 para a 1 (custo 10) = 11.
        // Ótimo: 0→1 (2) e 1→0 (3) = 5.
        var cost = new double[,] { { 1, 2 }, { 3, 10 } };
        Assert.Equal([1, 0], EmbarkAnalyzer.Hungarian(cost));
    }

    [Fact]
    public void Hungarian_handles_fewer_rows_than_columns()
    {
        var cost = new double[,] { { 5, 1, 9 } };
        Assert.Equal([1], EmbarkAnalyzer.Hungarian(cost));
    }

    private static readonly EmbarkRole Miner = new()
    {
        Id = "minerador", Name = "Minerador", Skill = "MINING", Priority = 1, Attributes = ["STRENGTH"],
    };

    private static readonly EmbarkRole Doctor = new()
    {
        Id = "medico", Name = "Médico", Skill = "DIAGNOSE", Priority = 2, Attributes = ["ANALYTICAL_ABILITY"],
    };

    [Fact]
    public void Analyze_gives_each_dwarf_one_primary_role_by_attributes()
    {
        var snapshot = EmbarkJson.Parse(SampleJson(dwarves: 2));
        var analysis = EmbarkAnalyzer.Analyze(snapshot, [Miner, Doctor]);

        // Anão 1 é mais forte; anão 0 tem mais análise (e já minera, mas a diferença de atributos pesa mais).
        var primary = analysis.Assignments.Where(a => a.IsPrimary).ToDictionary(a => a.Role.Id, a => a.Dwarf.Index);
        Assert.Equal(1, primary["minerador"]);
        Assert.Equal(0, primary["medico"]);
        Assert.All(analysis.Assignments, a => Assert.Equal(5, a.SuggestedLevel));
    }

    [Fact]
    public void Analyze_prefers_dwarf_that_already_has_the_skill_when_close()
    {
        var snapshot = EmbarkJson.Parse(SampleJson(dwarves: 2));
        var sameStrength = new EmbarkSnapshot
        {
            State = EmbarkScreenState.Embark,
            AttributeRanges = snapshot.AttributeRanges,
            Dwarves =
            [
                new EmbarkDwarf { Index = 0, Skills = [new EmbarkSkill("MINING", 3)], Physical = new Dictionary<string, int> { ["STRENGTH"] = 1240 } },
                new EmbarkDwarf { Index = 1, Physical = new Dictionary<string, int> { ["STRENGTH"] = 1260 } },
            ],
        };

        var analysis = EmbarkAnalyzer.Analyze(sameStrength, [Miner]);
        Assert.Equal(0, analysis.Assignments.Single().Dwarf.Index);
    }

    [Fact]
    public void Analyze_never_gives_the_same_role_twice_to_a_dwarf()
    {
        var twoMiners = Miner with { Count = 2 };
        var snapshot = EmbarkJson.Parse(SampleJson(dwarves: 1));
        var analysis = EmbarkAnalyzer.Analyze(snapshot, [twoMiners, Doctor]);

        Assert.NotEmpty(analysis.Assignments);
        Assert.Equal(analysis.Assignments.Count, analysis.Assignments.Select(a => a.Role.Id).Distinct().Count());
    }

    [Fact]
    public void Default_roles_load_and_use_known_attributes()
    {
        var known = DfNames.PhysicalAttributes.Concat(DfNames.MentalAttributes).ToHashSet();
        Assert.NotEmpty(EmbarkRoles.Default);
        Assert.All(EmbarkRoles.Default, r =>
        {
            Assert.NotEmpty(r.Attributes);
            Assert.All(r.Attributes, a => Assert.Contains(a, known));
            Assert.NotEqual(r.Skill, DfNames.Skill(r.Skill)); // toda habilidade usada tem nome em pt-BR
        });
    }

    [Fact]
    public void PersonalityNotes_flags_extremes()
    {
        var d = new EmbarkDwarf { Traits = new Dictionary<string, int> { ["STRESS_VULNERABILITY"] = 90, ["BRAVERY"] = 10, ["ANXIETY_PROPENSITY"] = 50 } };
        Assert.Equal(["estressa fácil", "medroso"], EmbarkAnalyzer.PersonalityNotes(d));
    }

    [Fact]
    public void Skill_name_falls_back_to_game_caption()
    {
        var captions = new Dictionary<string, string> { ["MODSKILL01"] = "Mod Skill 1" };
        Assert.Equal("Mineração", DfNames.Skill("MINING", captions));
        Assert.Equal("Mod Skill 1", DfNames.Skill("MODSKILL01", captions));
        Assert.Equal("XYZ", DfNames.Skill("XYZ", captions));
    }

    [Fact]
    public void Markdown_has_suggestion_and_parseable_tables()
    {
        var analysis = EmbarkAnalyzer.Analyze(EmbarkJson.Parse(SampleJson()));
        var md = EmbarkMarkdown.Build(analysis);

        Assert.Contains("## Sugestão de funções", md);
        Assert.Contains("**1400** restantes de 2256", md);
        Assert.Contains("estressa fácil, medroso", md);
        Assert.Contains("Mineração 2", md);

        var tables = MarkdownParser.Parse(md).OfType<MdTable>().ToList();
        Assert.True(tables.Count >= 5);
        Assert.All(tables, t => Assert.All(t.Rows, r => Assert.Equal(t.Headers.Count, r.Count)));
    }

    [Fact]
    public void DwarfSchema_columns_are_documented()
    {
        var schema = EmbarkMarkdown.DwarfSchema(EmbarkAnalyzer.Analyze(EmbarkJson.Parse(SampleJson())));
        Assert.All(schema.Columns, c => Assert.False(string.IsNullOrWhiteSpace(c.Description)));
        Assert.Contains(schema.Columns, c => c.Key == "strength");
    }
}
