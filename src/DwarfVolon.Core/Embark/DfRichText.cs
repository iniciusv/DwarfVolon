using System.Text;
using System.Text.RegularExpressions;

namespace DwarfVolon.Core.Embark;

/// <summary>Tom de um trecho de texto do jogo, a partir da cor que o DF usa.</summary>
public enum DfTextTone
{
    Normal,

    /// <summary>Verde no jogo (qualidades).</summary>
    Positive,

    /// <summary>Vermelho no jogo (defeitos).</summary>
    Negative,
}

/// <summary>Trecho com uma cor só. <see cref="Color"/> é o índice da paleta de 16 cores do DF (7 = cinza normal).</summary>
public sealed record DfTextRun(string Text, int Color)
{
    public DfTextTone Tone => (Color & 7) switch
    {
        2 => DfTextTone.Positive,
        4 => DfTextTone.Negative,
        _ => DfTextTone.Normal,
    };
}

public sealed record DfTextParagraph(IReadOnlyList<DfTextRun> Runs)
{
    public string PlainText => string.Concat(Runs.Select(r => r.Text)).Trim();
}

/// <summary>
/// Lê o texto com marcações que o DF usa nas fichas: <c>[C:frente:fundo:brilho]</c> muda a cor,
/// <c>[P]</c> começa parágrafo, <c>[B]</c> linha em branco, <c>[R]</c> quebra de linha.
/// </summary>
public static partial class DfRichText
{
    public const int DefaultColor = 7;

    [GeneratedRegex(@"\[(?<tag>[A-Z])(?::(?<args>-?\d+(?::-?\d+)*))?\]")]
    private static partial Regex TagRegex();

    public static IReadOnlyList<DfTextParagraph> Parse(string? text)
    {
        var paragraphs = new List<DfTextParagraph>();
        if (string.IsNullOrWhiteSpace(text)) return paragraphs;

        var runs = new List<DfTextRun>();
        var color = DefaultColor;
        var pos = 0;

        void Add(string piece)
        {
            if (piece.Length == 0) return;
            if (runs.Count > 0 && runs[^1].Color == color) runs[^1] = runs[^1] with { Text = runs[^1].Text + piece };
            else runs.Add(new DfTextRun(piece, color));
        }

        void Break()
        {
            var paragraph = new DfTextParagraph(Normalize(runs));
            if (paragraph.PlainText.Length > 0) paragraphs.Add(paragraph);
            runs = [];
        }

        foreach (Match m in TagRegex().Matches(text))
        {
            Add(text[pos..m.Index]);
            pos = m.Index + m.Length;
            switch (m.Groups["tag"].Value)
            {
                case "C":
                    var args = m.Groups["args"].Value.Split(':', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                    color = args.Length == 0 ? DefaultColor : args[0] + (args.Length >= 3 && args[2] != 0 ? 8 : 0);
                    break;
                case "P":
                case "B":
                case "R":
                    Break();
                    break;
            }
        }
        Add(text[pos..]);
        Break();
        return paragraphs;
    }

    /// <summary>Texto sem marcações, um parágrafo por linha (para Markdown e IA).</summary>
    public static string ToPlainText(string? text) =>
        string.Join("\n", Parse(text).Select(p => p.PlainText));

    // Junta espaços duplos e tira espaços das pontas do parágrafo.
    private static List<DfTextRun> Normalize(List<DfTextRun> runs)
    {
        var result = runs
            .Select(r => r with { Text = MultiSpace().Replace(r.Text, " ") })
            .Where(r => r.Text.Length > 0)
            .ToList();
        if (result.Count == 0) return result;
        result[0] = result[0] with { Text = result[0].Text.TrimStart() };
        result[^1] = result[^1] with { Text = result[^1].Text.TrimEnd() };
        return result.Where(r => r.Text.Length > 0).ToList();
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpace();
}

/// <summary>Texto de uma aba da ficha do jogo, guardado para um anão.</summary>
public sealed record CapturedSheet(int PersonalityTab, IReadOnlyList<string> Blocks, DateTime CapturedAt)
{
    public IReadOnlyList<DfTextParagraph> Paragraphs => Blocks.SelectMany(DfRichText.Parse).ToList();

    public string PlainText => string.Join("\n", Blocks.Select(DfRichText.ToPlainText).Where(t => t.Length > 0));
}

/// <summary>
/// Guarda o texto das fichas abertas no jogo, por unidade e aba de personalidade. O DF só gera esse
/// texto com a ficha aberta; com a atualização automática, basta abrir a ficha de cada anão uma vez.
/// </summary>
public sealed class EmbarkSheetCache
{
    private readonly Dictionary<int, SortedDictionary<int, CapturedSheet>> _sheets = [];

    /// <summary>Muda a cada texto novo ou diferente.</summary>
    public int Version { get; private set; }

    /// <summary>Guarda a ficha aberta da leitura, se for a aba de personalidade. Devolve true se mudou algo.</summary>
    public bool Update(EmbarkSnapshot snapshot)
    {
        if (snapshot.Sheet is not { IsPersonality: true } sheet) return false;

        if (!_sheets.TryGetValue(sheet.UnitId, out var tabs)) _sheets[sheet.UnitId] = tabs = [];
        if (tabs.TryGetValue(sheet.PersonalityTab, out var old) && old.Blocks.SequenceEqual(sheet.Blocks)) return false;

        tabs[sheet.PersonalityTab] = new CapturedSheet(sheet.PersonalityTab, sheet.Blocks, DateTime.Now);
        Version++;
        return true;
    }

    public IReadOnlyList<CapturedSheet> For(int? unitId) =>
        unitId is { } id && _sheets.TryGetValue(id, out var tabs) ? tabs.Values.ToList() : [];

    /// <summary>Texto puro de todas as abas guardadas do anão (vazio se nenhuma).</summary>
    public string PlainTextFor(int? unitId)
    {
        var builder = new StringBuilder();
        foreach (var sheet in For(unitId))
        {
            var text = sheet.PlainText;
            if (text.Length == 0) continue;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(text);
        }
        return builder.ToString();
    }
}
