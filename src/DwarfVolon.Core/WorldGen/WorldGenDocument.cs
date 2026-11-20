using System.Text;

namespace DwarfVolon.Core.WorldGen;

/// <summary>
/// Representa o arquivo world_gen.txt inteiro. O parser preserva tudo que não entende
/// (comentários, linhas soltas, tokens desconhecidos, PS_*), então ler e salvar sem
/// alterações gera um arquivo byte a byte idêntico.
/// </summary>
public sealed class WorldGenDocument
{
    /// <summary>
    /// Latin-1 mapeia cada byte 0–255 para um caractere, então a leitura e a escrita
    /// preservam os bytes originais mesmo que o jogo use CP437.
    /// </summary>
    public static readonly Encoding FileEncoding = Encoding.Latin1;

    private WorldGenDocument(List<string> preamble, List<WorldGenParameterSet> sets, string newLine, bool endsWithNewLine)
    {
        Preamble = preamble;
        ParameterSets = sets;
        NewLine = newLine;
        EndsWithNewLine = endsWithNewLine;
    }

    /// <summary>Linhas antes do primeiro <c>[WORLD_GEN]</c>.</summary>
    public List<string> Preamble { get; }

    public List<WorldGenParameterSet> ParameterSets { get; }

    public string NewLine { get; }

    public bool EndsWithNewLine { get; }

    public static WorldGenDocument CreateEmpty() => new([], [], Environment.NewLine, true);

    public static WorldGenDocument Parse(string text)
    {
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var rawLines = text.Split('\n').Select(l => l.EndsWith('\r') ? l[..^1] : l).ToList();
        var endsWithNewLine = text.Length > 0 && text.EndsWith('\n');
        if (endsWithNewLine || text.Length == 0) rawLines.RemoveAt(rawLines.Count - 1);

        var preamble = new List<string>();
        var sets = new List<WorldGenParameterSet>();
        List<WorldGenLine>? current = null;

        foreach (var raw in rawLines)
        {
            var line = WorldGenLine.Parse(raw);
            if (line.Tokens.Any(t => t.Name == WorldGenParameterSet.HeaderToken))
            {
                current = [];
                sets.Add(new WorldGenParameterSet(raw, current));
                // Tokens na mesma linha do cabeçalho (raro) entram no bloco como linha própria.
                var extra = line.Tokens.Where(t => t.Name != WorldGenParameterSet.HeaderToken).ToList();
                if (extra.Count > 0)
                {
                    var extraLine = WorldGenLine.Create(extra[0], "\t");
                    extraLine.Tokens.AddRange(extra.Skip(1));
                    extraLine.Dirty = true;
                    current.Add(extraLine);
                }
                continue;
            }

            if (current is null) preamble.Add(raw);
            else current.Add(line);
        }

        return new WorldGenDocument(preamble, sets, newLine, endsWithNewLine || text.Length == 0);
    }

    public static WorldGenDocument Load(string path) => Parse(File.ReadAllText(path, FileEncoding));

    public string ToText()
    {
        var lines = Preamble.Concat(ParameterSets.SelectMany(s => s.RenderLines()));
        var body = string.Join(NewLine, lines);
        return EndsWithNewLine && (Preamble.Count > 0 || ParameterSets.Count > 0) ? body + NewLine : body;
    }

    public void Save(string path) => File.WriteAllText(path, ToText(), FileEncoding);

    public WorldGenParameterSet? FindByTitle(string title) =>
        ParameterSets.FirstOrDefault(s => string.Equals(s.Title, title, StringComparison.OrdinalIgnoreCase));

    /// <summary>Substitui o conjunto com o mesmo título ou adiciona ao final. Retorna true se substituiu.</summary>
    public bool Upsert(WorldGenParameterSet set)
    {
        var title = set.Title ?? throw new InvalidOperationException("O conjunto precisa de um TITLE.");
        var index = ParameterSets.FindIndex(s => string.Equals(s.Title, title, StringComparison.OrdinalIgnoreCase));
        var copy = set.Clone();

        if (index >= 0)
        {
            // Mantém a linha em branco que separava este bloco do próximo.
            if (ParameterSets[index].EndsWithBlankLine && !copy.EndsWithBlankLine) copy.AppendBlankLine();
            ParameterSets[index] = copy;
            return true;
        }

        if (ParameterSets.Count > 0 && !ParameterSets[^1].EndsWithBlankLine) ParameterSets[^1].AppendBlankLine();
        else if (ParameterSets.Count == 0 && Preamble.Count > 0 && !string.IsNullOrWhiteSpace(Preamble[^1])) Preamble.Add(string.Empty);
        ParameterSets.Add(copy);
        return false;
    }

    public bool RemoveByTitle(string title) =>
        ParameterSets.RemoveAll(s => string.Equals(s.Title, title, StringComparison.OrdinalIgnoreCase)) > 0;
}
