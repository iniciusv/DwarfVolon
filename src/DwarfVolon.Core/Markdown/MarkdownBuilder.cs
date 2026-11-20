using System.Text;

namespace DwarfVolon.Core.Markdown;

/// <summary>
/// Monta documentos Markdown. É o formato comum entre telas do DwarfVolon e a IA:
/// o mesmo texto é renderizado na interface e pode ser copiado/enviado como contexto.
/// </summary>
public sealed class MarkdownBuilder
{
    private readonly StringBuilder _sb = new();

    public MarkdownBuilder Heading(int level, string text) =>
        Block($"{new string('#', Math.Clamp(level, 1, 6))} {OneLine(text)}");

    public MarkdownBuilder Paragraph(string text) => string.IsNullOrWhiteSpace(text) ? this : Block(text.Trim());

    public MarkdownBuilder Bullets(IEnumerable<string> items)
    {
        var lines = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => $"- {OneLine(i)}").ToList();
        return lines.Count == 0 ? this : Block(string.Join('\n', lines));
    }

    public MarkdownBuilder Numbered(IEnumerable<string> items)
    {
        var lines = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select((i, n) => $"{n + 1}. {OneLine(i)}").ToList();
        return lines.Count == 0 ? this : Block(string.Join('\n', lines));
    }

    public MarkdownBuilder Quote(string text) =>
        string.IsNullOrWhiteSpace(text) ? this : Block(string.Join('\n', text.Trim().Split('\n').Select(l => $"> {l.TrimEnd('\r')}")));

    public MarkdownBuilder Code(string text, string? language = null) => Block($"```{language}\n{text.TrimEnd()}\n```");

    public MarkdownBuilder Rule() => Block("---");

    /// <summary>Tabela a partir de cabeçalhos e linhas já prontas.</summary>
    public MarkdownBuilder Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows,
        IReadOnlyList<MdAlign>? aligns = null, MarkdownTableOptions? options = null) =>
        Block(MarkdownTable.Render(headers, rows, aligns, options));

    /// <summary>Tabela a partir de um schema, com as colunas escolhidas.</summary>
    public MarkdownBuilder Table<T>(MarkdownTableSchema<T> schema, IEnumerable<T> rows, IEnumerable<string>? columnKeys = null,
        MarkdownTableOptions? options = null) =>
        Block(schema.Render(rows, columnKeys, options));

    /// <summary>Tabela de duas colunas "campo | valor".</summary>
    public MarkdownBuilder KeyValues(IEnumerable<(string Key, object? Value)> pairs, string keyHeader = "Campo", string valueHeader = "Valor") =>
        Table([keyHeader, valueHeader], pairs.Select(p => (IReadOnlyList<object?>)[p.Key, p.Value]));

    public MarkdownBuilder Raw(string markdown) => string.IsNullOrWhiteSpace(markdown) ? this : Block(markdown.Trim());

    public override string ToString() => _sb.ToString().TrimEnd() + "\n";

    private MarkdownBuilder Block(string text)
    {
        if (_sb.Length > 0) _sb.Append("\n\n");
        _sb.Append(text);
        return this;
    }

    private static string OneLine(string text) => text.Replace("\r\n", " ").Replace('\n', ' ').Trim();
}
