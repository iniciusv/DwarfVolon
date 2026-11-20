using DwarfVolon.Core.Markdown;

namespace DwarfVolon.Core.Embark;

/// <summary>
/// Documento Markdown do embarque: o mesmo texto aparece na tela e pode ser copiado para a IA.
/// </summary>
public static class EmbarkMarkdown
{
    /// <summary>Colunas possíveis da tabela de anões (para o seletor de colunas e para a IA).</summary>
    public static MarkdownTableSchema<EmbarkDwarf> DwarfSchema(EmbarkAnalysis analysis, EmbarkPlan? plan = null)
    {
        var s = analysis.Snapshot;
        var schema = new MarkdownTableSchema<EmbarkDwarf>()
            .Add("num", "#", d => d.Index + 1, MdAlign.Right, "Posição na lista do jogo")
            .Add("nome", "Nome", d => d.DisplayName, description: "Nome do anão")
            .Add("nome_en", "Nome (inglês)", d => d.NameEnglish, description: "Nome traduzido")
            .Add("sexo", "Sexo", d => DfNames.Sex(d.Sex), MdAlign.Center, "F ou M")
            .Add("idade", "Idade", d => s.AgeOf(d), MdAlign.Right, "Idade em anos")
            .Add("apelido", "Apelido", d => d.Nickname, description: "Apelido dado no jogo")
            .Add("habilidades", "Escolhidas", d => SkillList(d.Skills, s), description: "Habilidades escolhidas no embarque, com nível")
            .Add("ja_sabe", "Já sabe", d => SkillList(d.BackgroundSkills, s), description: "Habilidades que o anão já traz (não gastam pontos)")
            .Add("livres", "Pontos livres", d => d.PicksLeft, MdAlign.Right, "Pontos de habilidade que o anão ainda pode receber")
            .Add("funcoes", "Funções", d => RoleList(analysis, plan, d), description: "Funções definidas (pelo jogador, pela IA ou pelo botão Sugerir)")
            .Add("pontos", "Plano de pontos", d => PointsList(analysis, plan, d), description: "Níveis planejados para comprar no embarque")
            .Add("personalidade", "Personalidade", d => string.Join(", ", EmbarkAnalyzer.PersonalityNotes(d)),
                description: "Traços que pesam no humor (estresse, ansiedade...)")
            .Add("gostos", "Gostos", d => string.Join(", ", d.Preferences.Where(p => p.Kind != "HateCreature").Select(PreferenceText)),
                description: "O que o anão gosta (nomes do jogo, em inglês)")
            .Add("odeia", "Odeia", d => string.Join(", ", d.Preferences.Where(p => p.Kind == "HateCreature").Select(PreferenceText)),
                description: "Criaturas que o anão odeia")
            .Add("crencas", "Crenças", d => string.Join(", ", d.Beliefs.Where(b => Math.Abs(b.Strength) > 10)
                    .OrderByDescending(b => Math.Abs(b.Strength)).Select(b => $"{DfNames.Belief(b.Id)} {b.Strength:+0;-0}")),
                description: "Crenças pessoais fortes (-50 a 50; positivo = valoriza)")
            .Add("sonho", "Sonho", d => string.Join(", ", d.Dreams.Select(DfNames.Dream)), description: "Sonho de vida do anão");

        foreach (var attr in DfNames.PhysicalAttributes.Concat(DfNames.MentalAttributes))
            schema.Add(attr.ToLowerInvariant(), DfNames.Attribute(attr), d => analysis.AttributeScore(d, attr), MdAlign.Right,
                $"Nota de {DfNames.Attribute(attr)} (0–100, 50 = mediana da raça)");

        return schema;
    }

    /// <summary>
    /// Resumo para a IA. Não inclui as descrições capturadas das fichas do jogo: elas repetem o que
    /// os números já dizem e gastariam muito contexto (ficam só na ficha da tela).
    /// </summary>
    public static string Build(EmbarkAnalysis analysis, EmbarkPlan? plan = null)
    {
        var s = analysis.Snapshot;
        var md = new MarkdownBuilder().Heading(1, "Embarque");

        md.Paragraph($"**{s.Dwarves.Count} anões** · pontos de embarque: **{s.PointsRemaining}** restantes de {s.EmbarkPoints}");
        foreach (var warning in analysis.Warnings) md.Quote("⚠️ " + warning);

        if (s.Dwarves.Count == 0)
            return md.Paragraph("Nenhum anão na tela de embarque.").ToString();

        var schema = DwarfSchema(analysis, plan);
        md.Heading(2, "Anões")
          .Table(schema, s.Dwarves, ["nome", "sexo", "idade", "habilidades", "livres", "personalidade", "ja_sabe"]);

        if (plan is not null && s.Dwarves.Any(plan.Has))
            md.Heading(2, "Decisões até agora")
              .Paragraph("Funções e pontos já decididos (pelo jogador ou numa resposta anterior).")
              .Table(schema, s.Dwarves.Where(plan.Has), ["nome", "funcoes", "pontos"]);

        md.Heading(2, "Gostos, crenças e sonhos")
          .Table(schema, s.Dwarves, ["nome", "gostos", "odeia", "crencas", "sonho"]);

        md.Heading(2, "Nota por função")
          .Paragraph("Quanto cada anão combina com cada função (0–100): média dos atributos que ajudam na habilidade, " +
                     "comparados com a mediana da raça (50 = mediano). Em negrito, o melhor de cada coluna.");
        var roles = analysis.Roles;
        md.Table(
            ["Anão", .. roles.Select(r => r.Name)],
            s.Dwarves.Select(d => (IReadOnlyList<object?>)
            [
                d.DisplayName,
                .. roles.Select(r =>
                {
                    var fit = analysis.Fit(d, r);
                    return (object?)(analysis.Best(r).Dwarf.Index == d.Index ? $"**{fit.Score}**" : fit.Score.ToString());
                }),
            ]),
            [MdAlign.Left, .. roles.Select(_ => MdAlign.Right)]);

        md.Heading(2, "Atributos")
          .Paragraph("Nota de cada atributo (0–100, 50 = mediana da raça).")
          .Heading(3, "Físicos")
          .Table(schema, s.Dwarves, ["nome", .. DfNames.PhysicalAttributes.Select(a => a.ToLowerInvariant())])
          .Heading(3, "Mentais")
          .Table(schema, s.Dwarves, ["nome", .. DfNames.MentalAttributes.Select(a => a.ToLowerInvariant())]);

        md.Heading(2, "Funções consideradas")
          .Table(["Função", "Habilidade", "Atributos", "Motivo"],
              roles.Select(r => (IReadOnlyList<object?>)
              [
                  r.Count > 1 ? $"{r.Name} (×{r.Count})" : r.Name,
                  DfNames.Skill(r.Skill, s.SkillCaptions),
                  string.Join(", ", r.Attributes.Select(DfNames.Attribute)),
                  r.Why,
              ]));

        return md.ToString();
    }

    /// <summary>Documento para quando o jogo não está na tela de embarque.</summary>
    public static string NotOnEmbarkScreen(string? screen) => new MarkdownBuilder()
        .Heading(1, "Embarque")
        .Paragraph("O jogo não está na tela de preparação do embarque" +
                   (string.IsNullOrEmpty(screen) ? "." : $" (tela atual: `{screen}`)."))
        .Heading(2, "Como usar")
        .Numbered([
            "Abra o Dwarf Fortress com o **DFHack**.",
            "Escolha o local do embarque e clique em **Preparar com cuidado** (*Prepare carefully*).",
            "Volte aqui e clique em **Atualizar**. Com *Atualizar automaticamente* ligado, a tela acompanha as mudanças que você fizer no jogo.",
        ])
        .ToString();

    private static string PreferenceText(EmbarkPreference p) =>
        string.IsNullOrWhiteSpace(p.Text) ? DfNames.PreferenceKind(p.Kind).ToLowerInvariant() : p.Text;

    private static string SkillList(IReadOnlyList<EmbarkSkill> skills, EmbarkSnapshot s) =>
        skills.Count == 0 ? "-" : string.Join(", ", skills.Select(k => $"{DfNames.Skill(k.Id, s.SkillCaptions)} {k.Level}"));

    private static string RoleList(EmbarkAnalysis analysis, EmbarkPlan? plan, EmbarkDwarf d) =>
        plan is null ? "" : string.Join(", ", plan.RolesOf(d).Select(id => analysis.Roles.FirstOrDefault(r => r.Id == id)?.Name ?? id));

    private static string PointsList(EmbarkAnalysis analysis, EmbarkPlan? plan, EmbarkDwarf d) =>
        plan is null ? "" : string.Join(", ", plan.For(d).OrderByDescending(kv => kv.Value)
            .Select(kv => $"{DfNames.Skill(kv.Key, analysis.Snapshot.SkillCaptions)} {kv.Value}"));
}
