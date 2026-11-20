using System.Globalization;
using System.Text;

namespace DwarfVolon.Core.Markdown;

public enum MdAlign
{
    Left,
    Center,
    Right,
}

/// <summary>Uma coluna possível de uma tabela de <typeparamref name="T"/>.</summary>
public sealed record MarkdownColumn<T>(string Key, string Header, Func<T, object?> Value, MdAlign Align = MdAlign.Left, string? Description = null);

/// <summary>
/// Catálogo de colunas para um tipo de linha (ex.: anões). A UI deixa o usuário escolher quais
/// colunas quer (por <see cref="MarkdownColumn{T}.Key"/>) e a mesma tabela vai para a tela e para a IA.
/// </summary>
public sealed class MarkdownTableSchema<T>
{
    private readonly List<MarkdownColumn<T>> _columns = [];

    public IReadOnlyList<MarkdownColumn<T>> Columns => _columns;

    public MarkdownTableSchema<T> Add(string key, string header, Func<T, object?> value, MdAlign align = MdAlign.Left, string? description = null)
    {
        if (_columns.Any(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Coluna repetida: {key}", nameof(key));
        _columns.Add(new MarkdownColumn<T>(key, header, value, align, description));
        return this;
    }

    /// <summary>Renderiza as linhas com as colunas pedidas, na ordem pedida. Sem <paramref name="columnKeys"/>, usa todas.</summary>
    public string Render(IEnumerable<T> rows, IEnumerable<string>? columnKeys = null, MarkdownTableOptions? options = null)
    {
        var columns = columnKeys is null
            ? _columns
            : columnKeys.Select(k => _columns.FirstOrDefault(c => string.Equals(c.Key, k, StringComparison.OrdinalIgnoreCase))
                                     ?? throw new ArgumentException($"Coluna desconhecida: {k}", nameof(columnKeys)))
                        .ToList();

        return MarkdownTable.Render(
            columns.Select(c => c.Header).ToList(),
            rows.Select(r => (IReadOnlyList<object?>)columns.Select(c => c.Value(r)).ToList()),
            columns.Select(c => c.Align).ToList(),
            options);
    }

    /// <summary>Lista das colunas disponíveis, em Markdown (útil para a IA saber o que pode pedir).</summary>
    public string DescribeColumns() =>
        MarkdownTable.Render(["Campo", "Coluna", "Descrição"],
            _columns.Select(c => (IReadOnlyList<object?>)[c.Key, c.Header, c.Description ?? ""]));
}

public sealed record MarkdownTableOptions
{
    /// <summary>Alinha as colunas com espaços. Fica mais legível como texto puro, mas gasta mais contexto da IA.</summary>
    public bool Pad { get; init; }

    /// <summary>Texto para valores nulos ou vazios.</summary>
    public string Empty { get; init; } = "";

    public static MarkdownTableOptions Compact { get; } = new();
    public static MarkdownTableOptions Readable { get; } = new() { Pad = true };
}

public static class MarkdownTable
{
    public static string Render(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows,
        IReadOnlyList<MdAlign>? aligns = null,
        MarkdownTableOptions? options = null)
    {
        options ??= MarkdownTableOptions.Compact;
        var cells = rows.Select(r => headers.Select((_, i) => i < r.Count ? Format(r[i], options.Empty) : options.Empty).ToArray()).ToList();
        var head = headers.Select(h => Escape(h)).ToArray();
        var widths = head.Select((h, i) => options.Pad ? Math.Max(3, cells.Select(r => r[i].Length).Append(h.Length).Max()) : 0).ToArray();

        var sb = new StringBuilder();
        AppendRow(sb, head, widths, aligns);
        sb.Append('|');
        for (var i = 0; i < head.Length; i++)
        {
            var align = aligns is not null && i < aligns.Count ? aligns[i] : MdAlign.Left;
            // Compacto usa 3 traços (mínimo aceito por todos os parsers); com Pad, ocupa a largura da coluna.
            var width = options.Pad ? widths[i] + 2 : 3;
            var dashes = new string('-', width - (align == MdAlign.Center ? 2 : align == MdAlign.Right ? 1 : 0));
            sb.Append(align switch
            {
                MdAlign.Center => $":{dashes}:",
                MdAlign.Right => $"{dashes}:",
                _ => dashes,
            });
            sb.Append('|');
        }
        sb.Append('\n');
        foreach (var row in cells) AppendRow(sb, row, widths, aligns);
        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>Escapa o texto para caber numa célula: <c>|</c> vira <c>\|</c> e quebras de linha viram espaço.</summary>
    public static string Escape(string? text) =>
        string.IsNullOrEmpty(text)
            ? ""
            : text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace("|", "\\|").Trim();

    public static string Format(object? value, string empty = "") => value switch
    {
        null => empty,
        bool b => b ? "sim" : "não",
        string s => s.Length == 0 ? empty : Escape(s),
        IFormattable f => Escape(f.ToString(null, CultureInfo.InvariantCulture)),
        _ => Escape(value.ToString()),
    };

    private static void AppendRow(StringBuilder sb, IReadOnlyList<string> cells, int[] widths, IReadOnlyList<MdAlign>? aligns)
    {
        sb.Append('|');
        for (var i = 0; i < cells.Count; i++)
        {
            var align = aligns is not null && i < aligns.Count ? aligns[i] : MdAlign.Left;
            var text = widths[i] == 0 ? cells[i] : align == MdAlign.Right ? cells[i].PadLeft(widths[i]) : cells[i].PadRight(widths[i]);
            sb.Append(widths[i] == 0 ? text : $" {text} ").Append('|');
        }
        sb.Append('\n');
    }
}
