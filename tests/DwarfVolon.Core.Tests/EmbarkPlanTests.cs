using DwarfVolon.Core.Embark;

namespace DwarfVolon.Core.Tests;

public sealed class EmbarkPlanTests
{
    private static readonly string[] Names =
    [
        "Besmar Ustuthkifed", "Rakust Tishisïteb", "Kumil Sobìrrovod", "Tobul Mörulsedil",
        "Olon Berèrith", "Mosus Gïnonfeb", "èrith Eribzuden",
    ];

    /// <summary>Os 7 anões do embarque real usado no desenvolvimento, com as abas de habilidades do DF 53.17 (resumidas).</summary>
    internal static EmbarkSnapshot Snapshot() => new()
    {
        State = EmbarkScreenState.Embark,
        SkillTabs = new Dictionary<string, IReadOnlyList<string>>
        {
            ["CRUCIAL"] = ["MINING", "WOODCUTTING", "CARPENTRY", "MASONRY", "PLANT", "BREWING", "STONECRAFT"],
            ["LABOR"] = ["MECHANICS", "HERBALISM", "COOK", "ANIMALTRAIN"],
            ["COMBAT"] = ["AXE", "CROSSBOW", "SHIELD", "ARMOR"],
            ["OTHER"] = ["DRESS_WOUNDS", "DIAGNOSE", "SURGERY", "SET_BONE", "SUTURE", "NEGOTIATION", "JUDGING_INTENT",
                         "APPRAISAL", "ORGANIZATION", "RECORD_KEEPING"],
        },
        Dwarves = Names.Select((n, i) => new EmbarkDwarf { Index = i, UnitId = 11295 + i, Name = n, PicksLeft = 10 }).ToList(),
    };

    // Resposta no formato do protocolo, como uma IA daria (com texto antes e depois do bloco).
    private const string Response = """
        Segue a distribuição, poupando as habilidades que sobem rápido jogando:

        ```dwarfvolon-pontos
        Besmar Ustuthkifed: SET_BONE 5, SURGERY 4, CARPENTRY 1
        Rakust Tishisïteb: APPRAISAL 4, NEGOTIATION 3, JUDGING_INTENT 2, BREWING 1
        Kumil Sobìrrovod: MECHANICS 5, ANIMALTRAIN 3, MINING 1, STONECRAFT 1
        Tobul Mörulsedil: RECORD_KEEPING 4, ORGANIZATION 4, HERBALISM 1, WOODCUTTING 1
        Olon Berèrith: CROSSBOW 4, ARMOR 3, SHIELD 2, MASONRY 1
        Mosus Gïnonfeb: AXE 4, ARMOR 3, SHIELD 2, PLANT 1
        èrith Eribzuden: DIAGNOSE 5, SUTURE 2, DRESS_WOUNDS 2, MINING 1
        ```

        Mineração ficou com 1 ponto nos dois mineradores.
        """;

    [Fact]
    public void Parse_reads_the_block_for_every_dwarf()
    {
        var s = Snapshot();
        var import = EmbarkAiProtocol.Parse(Response, s);

        Assert.True(import.FoundBlock);
        Assert.Empty(import.Errors);
        Assert.Empty(import.Warnings);
        Assert.Equal(7, import.Levels.Count);
        Assert.Equal(5, import.Levels[0]["SET_BONE"]);
        Assert.Equal(1, import.Levels[2]["MINING"]);
        Assert.Equal(1, import.Levels[6]["MINING"]);
        Assert.All(import.Levels.Values, l => Assert.Equal(10, l.Values.Sum()));
    }

    [Fact]
    public void Parse_accepts_names_without_accents_pt_names_and_missing_fences()
    {
        var s = Snapshot();
        var import = EmbarkAiProtocol.Parse("""
            - **Erith Eribzuden**: Diagnóstico 5, Sutura 2
            Kumil: mineração 1, Mecânica 4
            """, s);

        Assert.False(import.FoundBlock);
        Assert.Empty(import.Errors);
        Assert.Equal(5, import.Levels[6]["DIAGNOSE"]);
        Assert.Equal(4, import.Levels[2]["MECHANICS"]);
    }

    [Fact]
    public void Parse_reports_unknown_names_skills_and_rule_breaks()
    {
        var s = Snapshot();
        var import = EmbarkAiProtocol.Parse("""
            ```dwarfvolon-pontos
            Urist McNinguém: MINING 1
            Besmar Ustuthkifed: DANCA_DO_VENTRE 2, SET_BONE 7
            Rakust Tishisïteb: APPRAISAL 5, NEGOTIATION 5, JUDGING_INTENT 5
            ```
            """, s);

        Assert.Contains(import.Errors, e => e.Contains("Urist McNinguém"));
        Assert.Contains(import.Errors, e => e.Contains("DANCA_DO_VENTRE"));
        Assert.Contains(import.Warnings, w => w.Contains("reduzido para 5"));
        Assert.Contains(import.Warnings, w => w.Contains("15 pontos") && w.Contains("limite é 10"));
        Assert.Equal(5, import.Levels[0]["SET_BONE"]); // o que deu para entender é usado
    }

    [Fact]
    public void Parse_without_any_plan_says_so()
    {
        var import = EmbarkAiProtocol.Parse("Claro! Posso ajudar. O que você quer fazer na fortaleza?", Snapshot());
        Assert.Empty(import.Levels);
        Assert.Contains(import.Errors, e => e.Contains("dwarfvolon-pontos"));
    }

    [Fact]
    public void Apply_replaces_only_listed_dwarves_and_format_round_trips()
    {
        var s = Snapshot();
        var plan = new EmbarkPlan();
        plan.Set(s.Dwarves[3], "COOK", 2); // Tobul tinha um plano
        plan.Set(s.Dwarves[0], "MINING", 5);

        EmbarkAiProtocol.Apply(EmbarkAiProtocol.Parse("```dwarfvolon-pontos\nBesmar Ustuthkifed: SET_BONE 5\n```", s), s, plan);

        Assert.Equal(new Dictionary<string, int> { ["SET_BONE"] = 5 }, plan.For(s.Dwarves[0]));
        Assert.Equal(2, plan.For(s.Dwarves[3])["COOK"]);

        var again = EmbarkAiProtocol.Parse(EmbarkAiProtocol.Format(s, plan), s);
        Assert.Empty(again.Errors);
        Assert.Equal(5, again.Levels[0]["SET_BONE"]);
        Assert.Equal(2, again.Levels[3]["COOK"]);
    }

    [Fact]
    public void Points_request_is_standalone_and_has_rules_and_format()
    {
        var s = Snapshot();
        var request = EmbarkAiProtocol.BuildPointsRequest(EmbarkAnalyzer.Analyze(s), "1 ponto só em mineração.");

        Assert.DoesNotContain("## Anões", request);        // o resumo vai separado
        Assert.DoesNotContain("## Atributos", request);
        Assert.Contains("> 1 ponto só em mineração.", request);
        Assert.Contains("Besmar Ustuthkifed 10", request);  // limites por anão
        Assert.Contains("Combate: AXE, CROSSBOW, SHIELD, ARMOR", request);
        Assert.Contains("```dwarfvolon-pontos", request);
    }

    [Fact]
    public void Parse_reads_roles_by_name_or_id_and_format_writes_them()
    {
        var s = Snapshot();
        var import = EmbarkAiProtocol.Parse("""
            ```dwarfvolon-pontos
            Besmar Ustuthkifed: SET_BONE 5 | funções: Médico, carpinteiro
            Kumil Sobìrrovod: MINING 1 | Funcoes: minerador, Astronauta
            Tobul Mörulsedil: RECORD_KEEPING 4
            ```
            """, s);

        Assert.Equal(["medico", "carpinteiro"], import.Roles[0]);
        Assert.Equal(["minerador"], import.Roles[2]);
        Assert.Contains(import.Errors, e => e.Contains("Astronauta"));
        Assert.False(import.Roles.ContainsKey(3)); // sem "| funções:", as funções ficam como estão

        var plan = new EmbarkPlan();
        plan.SetRoles(s.Dwarves[3], ["lenhador"]);
        EmbarkAiProtocol.Apply(import, s, plan);
        Assert.Equal(["lenhador"], plan.RolesOf(s.Dwarves[3]));
        Assert.Equal(PlanSource.Ai, plan.OriginOf(s.Dwarves[0])!.Source);

        var text = EmbarkAiProtocol.Format(s, plan);
        Assert.Contains("Besmar Ustuthkifed: SET_BONE 5 | funções: Médico, Carpinteiro", text);
        Assert.Equal(["medico", "carpinteiro"], EmbarkAiProtocol.Parse(text, s).Roles[0]);
    }

    [Fact]
    public void Store_round_trip_matches_by_historical_figure_then_name()
    {
        var dir = Directory.CreateTempSubdirectory("dwarfvolon-store").FullName;
        try
        {
            var s = Snapshot() is var snap ? new EmbarkSnapshot
            {
                State = snap.State, SkillTabs = snap.SkillTabs, SaveDir = "region2", World = "The Cyclopean Universe",
                Dwarves = snap.Dwarves.Select(d => new EmbarkDwarf
                {
                    Index = d.Index, UnitId = d.UnitId, HistFigureId = 94850 - d.Index, Name = d.Name, PicksLeft = d.PicksLeft,
                }).ToList(),
            } : null!;
            var plan = new EmbarkPlan();
            plan.Replace(s.Dwarves[0], new Dictionary<string, int> { ["SET_BONE"] = 5 }, PlanSource.Ai, ["medico"]);
            plan.SetRoles(s.Dwarves[6], ["minerador"]);

            var path = EmbarkPlanStore.PathFor(s, dir)!;
            Assert.EndsWith("region2.json", path);
            EmbarkPlanStore.Save(path, s, plan);

            // Depois do embarque as unidades podem ter outro id: casa pela figura histórica; sem ela, pelo nome.
            var later = new EmbarkSnapshot
            {
                State = EmbarkScreenState.Embark, SaveDir = "region2",
                Dwarves =
                [
                    new EmbarkDwarf { Index = 0, UnitId = 900, HistFigureId = 94850, Name = "Besmar Ustuthkifed" },
                    new EmbarkDwarf { Index = 1, UnitId = 901, Name = "Erith Eribzuden" },
                ],
            };
            var restored = new EmbarkPlan();
            Assert.Equal(2, EmbarkPlanStore.Load(path, later, restored));
            Assert.Equal(5, restored.For(later.Dwarves[0])["SET_BONE"]);
            Assert.Equal(PlanSource.Ai, restored.OriginOf(later.Dwarves[0])!.Source);
            Assert.Equal(["minerador"], restored.RolesOf(later.Dwarves[1]));

            Assert.Null(EmbarkPlanStore.PathFor(new EmbarkSnapshot(), dir)); // sem pasta do mundo, não salva
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Diff_lists_what_changes_in_the_game()
    {
        var s = Snapshot();
        var dwarf = new EmbarkDwarf { Index = 0, Name = "X", Skills = [new EmbarkSkill("MINING", 3), new EmbarkSkill("COOK", 1)] };
        var diff = EmbarkPlanRules.Diff(s, dwarf, new Dictionary<string, int> { ["MINING"] = 1, ["SET_BONE"] = 5 });

        Assert.Equal(["Imobilizar ossos 0 → 5", "Mineração 3 → 1", "Cozinha 1 → 0"], diff);
        Assert.Empty(EmbarkPlanRules.Diff(s, dwarf, new Dictionary<string, int> { ["MINING"] = 3, ["COOK"] = 1 }));
    }

    [Fact]
    public void Writer_sends_plan_json_before_the_script()
    {
        var code = DwarfVolon.DFHack.EmbarkWriter.BuildCode(
        [
            new EmbarkApplyItem(0, 11295, new Dictionary<string, int> { ["MINING"] = 1 }),
            new EmbarkApplyItem(1, null, new Dictionary<string, int>()),
        ]);

        Assert.StartsWith("local PLAN_JSON = [==[{\"dwarves\":[{\"index\":0,\"skills\":{\"MINING\":1},\"unit_id\":11295}", code);
        Assert.Contains("{\"index\":1,\"skills\":{}}", code); // sem unidade, sem unit_id
        Assert.Contains("skill_picks_left", code);
    }

    [Fact]
    public void Apply_result_parses_script_output()
    {
        Assert.True(EmbarkApplyResult.Parse("{\"ok\":true,\"applied\":[{\"index\":0,\"picks_left\":4}]}").Ok);
        var failed = EmbarkApplyResult.Parse("aviso\n{\"ok\":false,\"error\":\"o plano do anão 0 gasta 15 pontos\"}");
        Assert.False(failed.Ok);
        Assert.Contains("15 pontos", failed.Error);
    }

    [Fact]
    public void Plan_cost_and_checks()
    {
        var s = Snapshot();
        var dwarf = s.Dwarves[0];
        var plan = new EmbarkPlan();
        var changes = 0;
        plan.Changed += _ => changes++;

        plan.Set(dwarf, "MINING", 4);
        plan.Set(dwarf, "AXE", 5);
        plan.Set(dwarf, "COOK", 3);
        plan.Set(dwarf, "COOK", 0);

        Assert.Equal(9, plan.Cost(dwarf));
        Assert.Equal(4, changes);
        Assert.Empty(EmbarkPlanRules.Check(s, dwarf, plan.For(dwarf)));

        plan.Set(dwarf, "SET_BONE", 2);
        Assert.Contains(EmbarkPlanRules.Check(s, dwarf, plan.For(dwarf)), p => p.Contains("11 pontos"));
    }
}
