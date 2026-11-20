using DwarfVolon.Core.Markdown;
using DwarfVolon.Core.WorldGen;

namespace DwarfVolon.Core.Tests;

public class MarkdownTests
{
    private sealed record Dwarf(string Name, string Profession, int Mining, bool Idle, string Notes);

    private static readonly MarkdownTableSchema<Dwarf> Schema = new MarkdownTableSchema<Dwarf>()
        .Add("nome", "Nome", d => d.Name)
        .Add("prof", "Profissão", d => d.Profession)
        .Add("mineracao", "Mineração", d => d.Mining, MdAlign.Right, "Nível de habilidade (0–20)")
        .Add("ocioso", "Ocioso", d => d.Idle)
        .Add("obs", "Obs.", d => d.Notes);

    private static readonly Dwarf[] Dwarves =
    [
        new("Urist", "Minerador", 12, false, "gosta de | pipes"),
        new("Bomrek", "Pedreiro", 3, true, "linha\nquebrada"),
    ];

    [Fact]
    public void Tabela_Compacta_ComColunasEscolhidas()
    {
        var md = Schema.Render(Dwarves, ["nome", "mineracao", "ocioso"]);

        Assert.Equal(
            "|Nome|Mineração|Ocioso|\n" +
            "|---|--:|---|\n" +
            "|Urist|12|não|\n" +
            "|Bomrek|3|sim|", md);
    }

    [Fact]
    public void Tabela_EscapaPipeEQuebraDeLinha()
    {
        var md = Schema.Render(Dwarves, ["nome", "obs"]);
        Assert.Contains("|Urist|gosta de \\| pipes|", md);
        Assert.Contains("|Bomrek|linha quebrada|", md);
    }

    [Fact]
    public void Tabela_ComPad_AlinhaColunas()
    {
        var md = Schema.Render(Dwarves, ["nome", "mineracao"], MarkdownTableOptions.Readable);
        var lines = md.Split('\n');
        Assert.All(lines, l => Assert.Equal(lines[0].Length, l.Length));
        Assert.Equal("| Urist  |        12 |", lines[2]);
    }

    [Fact]
    public void Schema_ColunaDesconhecida_Falha()
    {
        Assert.Throws<ArgumentException>(() => Schema.Render(Dwarves, ["nada"]));
    }

    [Fact]
    public void Builder_E_Parser_FazemIdaEVolta()
    {
        var md = new MarkdownBuilder()
            .Heading(1, "Fortaleza")
            .Paragraph("Texto com **negrito** e `código`.")
            .Bullets(["um", "dois"])
            .Table(Schema, Dwarves, ["nome", "obs"])
            .Quote("cuidado")
            .Code("[RIVER_MINS:300:300]", "txt")
            .Rule()
            .ToString();

        var blocks = MarkdownParser.Parse(md);

        Assert.Collection(blocks,
            b => Assert.Equal(new MdHeading(1, "Fortaleza"), b),
            b => Assert.Equal("Texto com **negrito** e `código`.", Assert.IsType<MdParagraph>(b).Text),
            b => Assert.Equal(["um", "dois"], Assert.IsType<MdList>(b).Items),
            b =>
            {
                var table = Assert.IsType<MdTable>(b);
                Assert.Equal(["Nome", "Obs."], table.Headers);
                Assert.Equal(["Urist", "gosta de | pipes"], table.Rows[0]);
            },
            b => Assert.Equal("cuidado", Assert.IsType<MdQuote>(b).Text),
            b => Assert.Equal(new MdCode("[RIVER_MINS:300:300]", "txt"), b),
            b => Assert.IsType<MdRule>(b));
    }

    [Fact]
    public void Inlines_NegritoItalicoCodigo()
    {
        var inlines = MarkdownParser.ParseInlines("a **b** *c* `d`");
        Assert.Equal(
            [new MdInline("a "), new MdInline("b", Bold: true), new MdInline(" "), new MdInline("c", Italic: true), new MdInline(" "), new MdInline("d", Code: true)],
            inlines);
    }

    [Fact]
    public void WorldGen_Describe_ListaModificadoresEAlteracoes()
    {
        var categories = ModifierLibrary.LoadBuiltInCategories();
        var baseSet = BuiltInTemplates.Load().FindByTitle(BuiltInTemplates.DefaultTitle)!;
        var rios = categories.Single(c => c.Id == "rios");
        var working = baseSet.Clone();
        working.Title = "TESTE";
        PresetApplier.Apply(working, rios.FindOption("mais")!.ToPreset(rios));

        var md = WorldGenMarkdown.Describe(working, baseSet, categories.Select(c => (c, c == rios ? rios.FindOption("mais")! : c.DefaultOption)));

        Assert.StartsWith("# Mundo: TESTE", md);
        Assert.Contains("|Rios|**Mais rios**|", md);
        Assert.Contains("|Nascentes de rios|`RIVER_MINS`|100:100|300:300|", md);
        Assert.DoesNotContain("VOLCANO_MIN", md);
        Assert.Contains(MarkdownParser.Parse(md), b => b is MdTable { Headers: ["Parâmetro", "Token", "Base", "Novo"] });
    }

    [Fact]
    public void WorldGen_Catalogo_TemTodasAsCategorias()
    {
        var categories = ModifierLibrary.LoadBuiltInCategories();
        var md = WorldGenMarkdown.DescribeCatalog(categories, ModifierLibrary.LoadBuiltInProfiles());

        Assert.All(categories, c => Assert.Contains($"## {c.Name} (`{c.Id}`)", md));
        Assert.Contains("|rios-vulcoes|Rios e vulcões|rios=mais, vulcanismo=mais|", md);
    }
}
