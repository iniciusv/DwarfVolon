using System.Text;
using System.Text.RegularExpressions;

namespace DwarfVolon.Core.Markdown;

public abstract record MdBlock;
public sealed record MdHeading(int Level, string Text) : MdBlock;
public sealed record MdParagraph(string Text) : MdBlock;
public sealed record MdList(IReadOnlyList<string> Items, bool Ordered) : MdBlock;
public sealed record MdQuote(string Text) : MdBlock;
public sealed record MdCode(string Text, string? Language) : MdBlock;
public sealed record MdRule : MdBlock;
public sealed record MdTable(IReadOnlyList<string> Headers, IReadOnlyList<MdAlign> Aligns, IReadOnlyList<IReadOnlyList<string>> Rows) : MdBlock;

public sealed record MdInline(string Text, bool Bold = false, bool Italic = false, bool Code = false);

/// <summary>
/// Parser de Markdown simples (o subconjunto que o DwarfVolon gera): títulos, parágrafos,
/// listas, citações, blocos de código, linhas horizontais e tabelas no estilo GFM.
/// Inline: <c>**negrito**</c>, <c>*itálico*</c> e <c>`código`</c>.
/// </summary>
public static partial class MarkdownParser
{
    public static IReadOnlyList<MdBlock> Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var blocks = new List<MdBlock>();
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.Length == 0) { i++; continue; }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var language = trimmed[3..].Trim();
                var code = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal)) code.Add(lines[i++]);
                i++;
                blocks.Add(new MdCode(string.Join('\n', code), language.Length == 0 ? null : language));
                continue;
            }

            if (HeadingRegex().Match(trimmed) is { Success: true } heading)
            {
                blocks.Add(new MdHeading(heading.Groups[1].Length, heading.Groups[2].Value.Trim()));
                i++;
                continue;
            }

            if (RuleRegex().IsMatch(trimmed)) { blocks.Add(new MdRule()); i++; continue; }

            if (IsTableRow(trimmed) && i + 1 < lines.Length && SeparatorRegex().IsMatch(lines[i + 1].Trim()))
            {
                var headers = SplitRow(trimmed);
                var aligns = SplitRow(lines[i + 1].Trim()).Select(ParseAlign).ToList();
                var rows = new List<IReadOnlyList<string>>();
                i += 2;
                while (i < lines.Length && IsTableRow(lines[i].Trim()))
                {
                    var cells = SplitRow(lines[i].Trim()).ToList();
                    while (cells.Count < headers.Count) cells.Add("");
                    rows.Add(cells.Take(headers.Count).ToList());
                    i++;
                }
                while (aligns.Count < headers.Count) aligns.Add(MdAlign.Left);
                blocks.Add(new MdTable(headers, aligns, rows));
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                var quote = new List<string>();
                while (i < lines.Length && lines[i].Trim().StartsWith('>')) quote.Add(lines[i++].Trim()[1..].Trim());
                blocks.Add(new MdQuote(string.Join(' ', quote)));
                continue;
            }

            if (ListItemRegex().Match(trimmed) is { Success: true } firstItem)
            {
                var ordered = char.IsDigit(firstItem.Groups[1].Value[0]);
                var items = new List<string>();
                while (i < lines.Length && ListItemRegex().Match(lines[i].Trim()) is { Success: true } item)
                {
                    items.Add(item.Groups[2].Value.Trim());
                    i++;
                }
                blocks.Add(new MdList(items, ordered));
                continue;
            }

            var paragraph = new List<string>();
            while (i < lines.Length && lines[i].Trim().Length > 0 && !StartsBlock(lines, i)) paragraph.Add(lines[i++].Trim());
            blocks.Add(new MdParagraph(string.Join(' ', paragraph)));
        }

        return blocks;
    }

    /// <summary>Divide uma linha de tabela em células, respeitando <c>\|</c>.</summary>
    public static IReadOnlyList<string> SplitRow(string line)
    {
        var text = line.Trim();
        if (text.StartsWith('|')) text = text[1..];
        if (text.EndsWith('|') && !text.EndsWith("\\|", StringComparison.Ordinal)) text = text[..^1];

        var cells = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '|') { current.Append('|'); i++; }
            else if (text[i] == '|') { cells.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(text[i]);
        }
        cells.Add(current.ToString().Trim());
        return cells;
    }

    public static IReadOnlyList<MdInline> ParseInlines(string text)
    {
        var result = new List<MdInline>();
        var last = 0;
        foreach (Match m in InlineRegex().Matches(text))
        {
            if (m.Index > last) result.Add(new MdInline(text[last..m.Index]));
            if (m.Groups["code"].Success) result.Add(new MdInline(m.Groups["code"].Value, Code: true));
            else if (m.Groups["bold"].Success) result.Add(new MdInline(m.Groups["bold"].Value, Bold: true));
            else result.Add(new MdInline(m.Groups["italic"].Value, Italic: true));
            last = m.Index + m.Length;
        }
        if (last < text.Length) result.Add(new MdInline(text[last..]));
        return result;
    }

    private static bool StartsBlock(string[] lines, int i)
    {
        var t = lines[i].Trim();
        return t.StartsWith("```", StringComparison.Ordinal) || HeadingRegex().IsMatch(t) || RuleRegex().IsMatch(t) ||
               t.StartsWith('>') || ListItemRegex().IsMatch(t) ||
               (IsTableRow(t) && i + 1 < lines.Length && SeparatorRegex().IsMatch(lines[i + 1].Trim()));
    }

    private static bool IsTableRow(string line) => line.StartsWith('|') && line.Length > 1;

    private static MdAlign ParseAlign(string cell)
    {
        var c = cell.Trim();
        return c.StartsWith(':') && c.EndsWith(':') ? MdAlign.Center : c.EndsWith(':') ? MdAlign.Right : MdAlign.Left;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex RuleRegex();

    [GeneratedRegex(@"^\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?$")]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(@"^([-*+]|\d+[.)])\s+(.*)$")]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"`(?<code>[^`]+)`|\*\*(?<bold>.+?)\*\*|\*(?<italic>[^*\s][^*]*?)\*")]
    private static partial Regex InlineRegex();
}
