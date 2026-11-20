using DwarfVolon.Core.Markdown;

namespace DwarfVolon.Core.Embark;

/// <summary>
/// Documento Markdown do embarque: o mesmo texto aparece na tela e pode ser copiado para a IA.
/// </summary>
public static class EmbarkMarkdown
{
    /// <summary>Colunas possíveis da tabela de anões (para o seletor de colunas e para a IA).</summary>
    public static MarkdownTableSchema<EmbarkDwarf> DwarfSchema(EmbarkAnalysis analysis)
    {
        var s = analysis.Snapshot;
        var schema = new MarkdownTableSchema<EmbarkDwarf>()
            .Add("num", "#", d => d.Index + 1, MdAlign.Right, "Posição na lista do jogo")
            .Add("nome", "Nome", d => d.DisplayName, description: "Nome do anão")
            .Add("nome_en", "Nome (inglês)", d => d.NameEnglish, description: "Nome traduzido")
            .Add("sexo", "Sexo", d => DfNames.Sex(d.Sex), MdAlign.Center, "F ou M")
            .Add("idade", "Idade", d => s.AgeOf(d), MdAlign.Right, "Idade em anos")
            .Add("habilidades", "Habilidades atuais", d => SkillList(d, s), description: "Habilidades escolhidas agora, com nível")
            .Add("livres", "Pontos livres", d => d.PicksLeft, MdAlign.Right, "Pontos de habilidade que o anão ainda pode receber")
            .Add("plano", "Plano sugerido", d => Plan(analysis, d), description: "Funções e níveis sugeridos")
            .Add("personalidade", "Personalidade", d => string.Join(", ", EmbarkAnalyzer.PersonalityNotes(d)),
                description: "Traços que pesam no humor (estresse, ansiedade...)");

        foreach (var attr in DfNames.PhysicalAttributes.Concat(DfNames.MentalAttributes))
            schema.Add(attr.ToLowerInvariant(), DfNames.Attribute(attr), d => analysis.AttributeScore(d, attr), MdAlign.Right,
                $"Nota de {DfNames.Attribute(attr)} (0–100, 50 = mediana da raça)");

        return schema;
    }

    public static string Build(EmbarkAnalysis analysis)
    {
        var s = analysis.Snapshot;
        var md = new MarkdownBuilder().Heading(1, "Embarque");

        md.Paragraph($"**{s.Dwarves.Count} anões** · pontos de embarque: **{s.PointsRemaining}** restantes de {s.EmbarkPoints}");
        foreach (var warning in analysis.Warnings) md.Quote("⚠️ " + warning);

        if (s.Dwarves.Count == 0)
            return md.Paragraph("Nenhum anão na tela de embarque.").ToString();

        md.Heading(2, "Sugestão de funções")
          .Table(["Função", "Anão", "Nota", "Nível atual", "Sugerido", "Por quê"],
              analysis.Assignments.Select(a => (IReadOnlyList<object?>)
              [
                  a.IsPrimary ? $"**{a.Role.Name}**" : a.Role.Name,
                  a.Dwarf.DisplayName,
                  a.Fit.Score,
                  a.Fit.CurrentLevel > 0 ? $"{a.Fit.CurrentLevel} ({DfNames.SkillLevel(a.Fit.CurrentLevel)})" : "-",
                  a.SuggestedLevel > 0 ? $"{a.SuggestedLevel} ({DfNames.SkillLevel(a.SuggestedLevel)})" : "-",
                  Reason(a.Fit),
              ]),
              [MdAlign.Left, MdAlign.Left, MdAlign.Right, MdAlign.Left, MdAlign.Left, MdAlign.Left])
          .Paragraph("Em **negrito**, a função principal de cada anão. A **nota** (0–100) é a média dos atributos que ajudam na " +
                     "habilidade, comparados com a mediana da raça (50 = mediano). Os níveis sugeridos usam todos os pontos de " +
                     $"habilidade do anão (os livres mais os já gastos), até {EmbarkAnalyzer.MaxEmbarkLevel} (Proficiente) por habilidade.");

        md.Heading(2, "Anões")
          .Table(DwarfSchema(analysis), s.Dwarves, ["num", "nome", "sexo", "idade", "habilidades", "livres", "plano", "personalidade"]);

        md.Heading(2, "Nota por função")
          .Paragraph("Quanto cada anão combina com cada função (0–100). Em negrito, o melhor de cada coluna.");
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
          .Table(DwarfSchema(analysis), s.Dwarves, ["nome", .. DfNames.PhysicalAttributes.Select(a => a.ToLowerInvariant())])
          .Heading(3, "Mentais")
          .Table(DwarfSchema(analysis), s.Dwarves, ["nome", .. DfNames.MentalAttributes.Select(a => a.ToLowerInvariant())]);

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

    private static string SkillList(EmbarkDwarf d, EmbarkSnapshot s) =>
        d.Skills.Count == 0 ? "-" : string.Join(", ", d.Skills.Select(k => $"{DfNames.Skill(k.Id, s.SkillCaptions)} {k.Level}"));

    private static string Plan(EmbarkAnalysis analysis, EmbarkDwarf d) =>
        string.Join(" · ", analysis.AssignmentsFor(d).Select(a => $"{a.Role.Name} {a.SuggestedLevel}"));

    private static string Reason(RoleFit fit)
    {
        var parts = new List<string>();
        if (fit.Strengths.Count > 0)
            parts.Add("bom em " + string.Join(", ", fit.Strengths.Take(3).Select(a => $"{DfNames.Attribute(a.Attribute)} ({a.Score})")));
        if (fit.Weaknesses.Count > 0)
            parts.Add("fraco em " + string.Join(", ", fit.Weaknesses.Take(2).Select(a => $"{DfNames.Attribute(a.Attribute)} ({a.Score})")));
        if (fit.CurrentLevel > 0) parts.Add("já tem a habilidade");
        return parts.Count == 0 ? "atributos medianos" : string.Join("; ", parts);
    }
}
