using DwarfVolon.Core.Markdown;

namespace DwarfVolon.Core.WorldGen;

/// <summary>Descrições em Markdown do world gen, para mostrar na tela e mandar para a IA.</summary>
public static class WorldGenMarkdown
{
    /// <summary>Resumo de um conjunto: base, nível escolhido em cada categoria e parâmetros alterados.</summary>
    public static string Describe(
        WorldGenParameterSet set,
        WorldGenParameterSet baseSet,
        IEnumerable<(ModifierCategory Category, ModifierOption Option)> selections)
    {
        var md = new MarkdownBuilder()
            .Heading(1, $"Mundo: {set.Title ?? "(sem título)"}")
            .KeyValues(
            [
                ("Base", baseSet.Title),
                ("Tamanho", set.Dimensions is { } d ? $"{d.Width}×{d.Height}" : "?"),
            ]);

        md.Heading(2, "Modificadores")
          .Table(["Categoria", "Escolha", "Efeito"],
              selections.Select(s => (IReadOnlyList<object?>)[s.Category.Name, s.Option.IsDefault ? s.Option.Name : $"**{s.Option.Name}**", s.Option.Description]));

        var changes = Changes(set, baseSet).ToList();
        md.Heading(2, "Parâmetros alterados");
        if (changes.Count == 0) md.Paragraph("Nenhum: igual à base.");
        else md.Table(["Parâmetro", "Token", "Base", "Novo"], changes, [MdAlign.Left, MdAlign.Left, MdAlign.Right, MdAlign.Right]);

        var risks = WorldGenRiskEstimator.Estimate(set);
        md.Heading(2, $"Risco de rejeição (estimativa): {WorldGenRiskEstimator.Describe(WorldGenRiskEstimator.Overall(risks))}");
        var attention = risks.Where(r => r.Level > RiskLevel.Low).ToList();
        if (attention.Count == 0) md.Paragraph("Os mínimos exigidos cabem com folga na distribuição esperada.");
        else md.Table(["Item", "Risco", "Detalhe"], attention.Select(r => (IReadOnlyList<object?>)[r.Label, WorldGenRiskEstimator.Describe(r.Level), r.Detail]));

        return md.ToString();
    }

    /// <summary>Catálogo de categorias e níveis disponíveis: diz à IA o que ela pode escolher.</summary>
    public static string DescribeCatalog(IEnumerable<ModifierCategory> categories, IEnumerable<WorldGenProfile>? profiles = null)
    {
        var md = new MarkdownBuilder()
            .Heading(1, "Modificadores de world gen disponíveis")
            .Paragraph("Escolha um nível por categoria. `id` é o identificador usado em perfis e comandos.");

        foreach (var category in categories)
        {
            md.Heading(2, $"{category.Name} (`{category.Id}`)")
              .Paragraph(category.Description)
              .Table(["id", "Nível", "Efeito"],
                  category.Options.Select(o => (IReadOnlyList<object?>)[o.Id, o.IsDefault ? $"{o.Name} (padrão)" : o.Name, o.Description]));
        }

        if (profiles is not null)
        {
            md.Heading(2, "Perfis prontos")
              .Table(["id", "Perfil", "Escolhas"],
                  profiles.Select(p => (IReadOnlyList<object?>)[p.Id, p.Name,
                      p.Selections.Count == 0 ? "tudo padrão" : string.Join(", ", p.Selections.Select(s => $"{s.Key}={s.Value}"))]));
        }

        return md.ToString();
    }

    private static IEnumerable<IReadOnlyList<object?>> Changes(WorldGenParameterSet set, WorldGenParameterSet baseSet)
    {
        foreach (var token in set.Tokens.Where(t => t.Name != "TITLE"))
        {
            var info = WorldGenTokenCatalog.Describe(token);
            if (!info.IsEditable) continue;
            var before = baseSet.Find(token.Name, token.Key);
            if (before is not null && before.Values.SequenceEqual(token.Values)) continue;
            var label = token.Key is null ? info.DisplayName : $"{info.DisplayName}: {token.Key}";
            var name = token.Key is null ? token.Name : $"{token.Name}:{token.Key}";
            yield return [label, $"`{name}`", before is null ? "-" : string.Join(':', before.Values), string.Join(':', token.Values)];
        }
    }
}
