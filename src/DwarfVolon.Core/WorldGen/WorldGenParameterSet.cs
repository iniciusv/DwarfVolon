using System.Text.RegularExpressions;

namespace DwarfVolon.Core.WorldGen;

/// <summary>
/// Uma linha do arquivo. Linhas não alteradas são reescritas exatamente como foram lidas,
/// o que garante que salvar sem mudanças gera um arquivo idêntico.
/// </summary>
internal sealed partial class WorldGenLine
{
    private WorldGenLine(string raw, string indent, List<WorldGenToken> tokens)
    {
        Raw = raw;
        Indent = indent;
        Tokens = tokens;
    }

    public string Raw { get; }
    public string Indent { get; }
    public List<WorldGenToken> Tokens { get; }
    public bool Dirty { get; set; }

    public bool IsBlank => Tokens.Count == 0 && string.IsNullOrWhiteSpace(Raw);

    public string Render() => Dirty ? Indent + string.Concat(Tokens.Select(t => t.ToText())) : Raw;

    public static WorldGenLine Parse(string raw)
    {
        var indent = raw[..(raw.Length - raw.TrimStart().Length)];
        var tokens = TokenRegex().Matches(raw).Select(m => WorldGenToken.Parse(m.Groups[1].Value)).ToList();
        return new WorldGenLine(raw, indent, tokens);
    }

    public static WorldGenLine Create(WorldGenToken token, string indent) =>
        new(indent + token.ToText(), indent, [token]);

    public WorldGenLine Clone() => new(Raw, Indent, [.. Tokens]) { Dirty = Dirty };

    [GeneratedRegex(@"\[([^\[\]]*)\]")]
    internal static partial Regex TokenRegex();
}

/// <summary>
/// Um bloco <c>[WORLD_GEN]</c>: um conjunto de parâmetros que aparece na tela
/// "Design New World with Advanced Parameters" do jogo.
/// </summary>
public sealed class WorldGenParameterSet
{
    public const string HeaderToken = "WORLD_GEN";
    private const string DefaultIndent = "\t";

    internal WorldGenParameterSet(string headerRaw, List<WorldGenLine> lines)
    {
        HeaderRaw = headerRaw;
        Lines = lines;
    }

    internal string HeaderRaw { get; }
    internal List<WorldGenLine> Lines { get; }

    public static WorldGenParameterSet Create(string title)
    {
        var set = new WorldGenParameterSet($"[{HeaderToken}]", []);
        set.Title = title;
        return set;
    }

    public IEnumerable<WorldGenToken> Tokens => Lines.SelectMany(l => l.Tokens);

    public string? Title
    {
        get => Find("TITLE")?.Args.FirstOrDefault();
        set
        {
            if (string.IsNullOrWhiteSpace(value)) Remove("TITLE");
            else Set(new WorldGenToken("TITLE", SanitizeText(value)));
        }
    }

    public (int Width, int Height)? Dimensions
    {
        get
        {
            var dim = Find("DIM");
            return dim is { Args.Count: >= 2 } &&
                   int.TryParse(dim.Args[0], out var w) && int.TryParse(dim.Args[1], out var h)
                ? (w, h)
                : null;
        }
    }

    public WorldGenToken? Find(string name, string? key = null) =>
        Tokens.FirstOrDefault(t => t.Matches(name, key));

    /// <summary>Substitui o token com a mesma identidade (nome + chave) ou insere na ordem canônica do jogo.</summary>
    public void Set(WorldGenToken token)
    {
        foreach (var line in Lines)
        {
            var index = line.Tokens.FindIndex(t => t.Matches(token.Name, token.Key));
            if (index < 0) continue;
            if (!line.Tokens[index].Equals(token))
            {
                line.Tokens[index] = token;
                line.Dirty = true;
            }
            return;
        }

        Insert(token);
    }

    public void Set(string name, params string[] args) => Set(new WorldGenToken(name, args));

    public bool Remove(string name, string? key = null)
    {
        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            var index = line.Tokens.FindIndex(t => t.Matches(name, key));
            if (index < 0) continue;
            line.Tokens.RemoveAt(index);
            if (line.Tokens.Count == 0) Lines.RemoveAt(i);
            else line.Dirty = true;
            return true;
        }
        return false;
    }

    public WorldGenParameterSet Clone() => new(HeaderRaw, Lines.Select(l => l.Clone()).ToList());

    /// <summary>Texto do bloco, começando pela linha <c>[WORLD_GEN]</c>.</summary>
    public string ToText(string newLine = "\n") =>
        string.Join(newLine, RenderLines().SkipLast(CountTrailingBlankLines()));

    internal IEnumerable<string> RenderLines()
    {
        yield return HeaderRaw;
        foreach (var line in Lines) yield return line.Render();
    }

    internal bool EndsWithBlankLine => Lines.Count > 0 && Lines[^1].IsBlank;

    internal void AppendBlankLine() => Lines.Add(WorldGenLine.Parse(string.Empty));

    private int CountTrailingBlankLines()
    {
        var count = 0;
        for (var i = Lines.Count - 1; i >= 0 && Lines[i].IsBlank; i--) count++;
        return count;
    }

    private void Insert(WorldGenToken token)
    {
        var order = WorldGenTokenCatalog.CanonicalOrder(token.Name);
        var insertAt = -1;
        var lastTokenLine = -1;
        for (var i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].Tokens.Count == 0) continue;
            lastTokenLine = i;
            if (Lines[i].Tokens.Any(t => WorldGenTokenCatalog.CanonicalOrder(t.Name) <= order)) insertAt = i;
        }

        if (insertAt < 0) insertAt = lastTokenLine < 0 ? -1 : FirstTokenLine() - 1;
        var indent = lastTokenLine >= 0 ? Lines[lastTokenLine].Indent : DefaultIndent;
        if (indent.Length == 0) indent = DefaultIndent;
        Lines.Insert(insertAt + 1, WorldGenLine.Create(token, indent));
    }

    private int FirstTokenLine() => Lines.FindIndex(l => l.Tokens.Count > 0);

    /// <summary>Remove caracteres estruturais (<c>[ ] :</c>) e normaliza para maiúsculas, como o jogo faz.</summary>
    public static string SanitizeText(string text) =>
        new string(text.Where(c => c is not ('[' or ']' or ':') && !char.IsControl(c)).ToArray())
            .Trim().ToUpperInvariant();
}
