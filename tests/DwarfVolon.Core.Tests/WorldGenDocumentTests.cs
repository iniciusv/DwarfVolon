using DwarfVolon.Core.WorldGen;

namespace DwarfVolon.Core.Tests;

public class WorldGenDocumentTests
{
    private const string Sample =
        "; comentário antes do primeiro bloco\r\n" +
        "[WORLD_GEN]\r\n" +
        "\t[TITLE:TESTE]\r\n" +
        "\t[DIM:65:65]\r\n" +
        "\t[BOGEYMAN_NUMBER:14]\r\n" +
        "\t[VAMPIRE_NUMBER:14]\r\n" +
        "\t[REGION_COUNTS:SWAMP:260:1:1]\r\n" +
        "\t[REGION_COUNTS:DESERT:260:1:1]\r\n" +
        "\t[RIVER_MINS:100:100] texto solto preservado\r\n" +
        "\t[TOKEN_DESCONHECIDO:a:b]\r\n" +
        "\r\n" +
        "[WORLD_GEN]\r\n" +
        "\t[TITLE:OUTRO]\r\n" +
        "\t[DIM:17:17]\r\n";

    [Fact]
    public void RoundTrip_SemAlteracoes_GeraTextoIdentico()
    {
        Assert.Equal(Sample, WorldGenDocument.Parse(Sample).ToText());
    }

    [Fact]
    public void RoundTrip_PadroesEmbutidos_GeraTextoIdentico()
    {
        var original = ReadEmbeddedTemplateText();
        Assert.Equal(original, WorldGenDocument.Parse(original).ToText());
    }

    [Fact]
    public void Parse_EncontraConjuntosETitulos()
    {
        var doc = WorldGenDocument.Parse(Sample);
        Assert.Equal(["; comentário antes do primeiro bloco"], doc.Preamble);
        Assert.Equal(["TESTE", "OUTRO"], doc.ParameterSets.Select(s => s.Title));
        Assert.Equal((65, 65), doc.ParameterSets[0].Dimensions);
        Assert.Equal("\r\n", doc.NewLine);
    }

    [Fact]
    public void Set_AlteraSomenteALinhaDoToken()
    {
        var doc = WorldGenDocument.Parse(Sample);
        doc.ParameterSets[0].Set("RIVER_MINS", "300", "300");

        var expected = Sample.Replace("\t[RIVER_MINS:100:100] texto solto preservado", "\t[RIVER_MINS:300:300]");
        Assert.Equal(expected, doc.ToText());
    }

    [Fact]
    public void Set_TokenComChave_AlteraSoAquelaChave()
    {
        var set = WorldGenDocument.Parse(Sample).ParameterSets[0];
        set.Set("REGION_COUNTS", "DESERT", "500", "2", "2");

        Assert.Equal(["SWAMP", "260", "1", "1"], set.Find("REGION_COUNTS", "SWAMP")!.Args);
        Assert.Equal(["500", "2", "2"], set.Find("REGION_COUNTS", "DESERT")!.Values);
    }

    [Fact]
    public void Set_TokenNovo_EntraNaOrdemCanonica()
    {
        var set = WorldGenDocument.Parse(Sample).ParameterSets[0];
        set.Set("NIGHTMARE_NUMBER", "7");

        var names = set.Tokens.Select(t => t.Name).ToList();
        Assert.Equal(names.IndexOf("BOGEYMAN_NUMBER") + 1, names.IndexOf("NIGHTMARE_NUMBER"));
        Assert.Contains("\t[NIGHTMARE_NUMBER:7]", set.ToText());
    }

    [Fact]
    public void Upsert_SubstituiPorTituloEMantemOResto()
    {
        var doc = WorldGenDocument.Parse(Sample);
        var changed = doc.ParameterSets[0].Clone();
        changed.Set("DIM", "129", "129");

        Assert.True(doc.Upsert(changed));
        Assert.Equal(2, doc.ParameterSets.Count);
        Assert.Equal((129, 129), doc.FindByTitle("teste")!.Dimensions);
        Assert.Contains("\t[DIM:17:17]", doc.ToText());
        // A linha em branco entre os blocos continua lá.
        Assert.Contains("\r\n\r\n[WORLD_GEN]\r\n\t[TITLE:OUTRO]", doc.ToText());
    }

    [Fact]
    public void Upsert_TituloNovo_AdicionaAoFinalComLinhaEmBranco()
    {
        var doc = WorldGenDocument.Parse(Sample);
        var novo = doc.ParameterSets[1].Clone();
        novo.Title = "novo mundo";

        Assert.False(doc.Upsert(novo));
        Assert.EndsWith("\t[DIM:17:17]\r\n\r\n[WORLD_GEN]\r\n\t[TITLE:NOVO MUNDO]\r\n\t[DIM:17:17]\r\n", doc.ToText());
    }

    [Fact]
    public void Title_RemoveCaracteresEstruturais()
    {
        var set = WorldGenParameterSet.Create("meu [mundo]: legal");
        Assert.Equal("MEU MUNDO LEGAL", set.Title);
    }

    [Fact]
    public void DocumentoVazio_RecebeConjunto()
    {
        var doc = WorldGenDocument.CreateEmpty();
        var set = WorldGenParameterSet.Create("A");
        set.Set("DIM", "17", "17");
        doc.Upsert(set);

        var nl = Environment.NewLine;
        Assert.Equal($"[WORLD_GEN]{nl}\t[TITLE:A]{nl}\t[DIM:17:17]{nl}", doc.ToText());
    }

    [Fact]
    public void PadroesEmbutidos_TemDozeConjuntos()
    {
        var doc = BuiltInTemplates.Load();
        Assert.Equal(12, doc.ParameterSets.Count);
        Assert.Equal((129, 129), doc.FindByTitle("MEDIUM REGION")!.Dimensions);
    }

    internal static string ReadEmbeddedTemplateText()
    {
        using var stream = typeof(BuiltInTemplates).Assembly
            .GetManifestResourceStream("DwarfVolon.Core.WorldGen.Resources.df047_default_world_gen.txt")!;
        using var reader = new StreamReader(stream, WorldGenDocument.FileEncoding);
        return reader.ReadToEnd();
    }
}
