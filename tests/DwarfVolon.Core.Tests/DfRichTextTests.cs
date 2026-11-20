using DwarfVolon.Core.Embark;

namespace DwarfVolon.Core.Tests;

public sealed class DfRichTextTests
{
    // Trecho real da ficha do DF 53.17 (aba de personalidade).
    private const string Mental =
        "[C:2:0:0]She has an amazing spatial sense and a way with words, [C:4:0:0]but she has an iffy sense for music.  ";

    [Fact]
    public void Parse_splits_colors_into_tones()
    {
        var paragraph = Assert.Single(DfRichText.Parse(Mental));

        Assert.Equal(2, paragraph.Runs.Count);
        Assert.Equal(DfTextTone.Positive, paragraph.Runs[0].Tone);
        Assert.Equal(DfTextTone.Negative, paragraph.Runs[1].Tone);
        Assert.Equal("She has an amazing spatial sense and a way with words, but she has an iffy sense for music.", paragraph.PlainText);
    }

    [Fact]
    public void Paragraph_tags_break_and_bright_flag_is_kept()
    {
        var paragraphs = DfRichText.Parse("[P][C:7:0:1]She is friendly.[P]She can handle stress.[B]");

        Assert.Equal(["She is friendly.", "She can handle stress."], paragraphs.Select(p => p.PlainText));
        Assert.Equal(15, paragraphs[0].Runs[0].Color);
        Assert.Equal(DfTextTone.Normal, paragraphs[0].Runs[0].Tone);
    }

    [Fact]
    public void Empty_or_blank_gives_nothing()
    {
        Assert.Empty(DfRichText.Parse(""));
        Assert.Empty(DfRichText.Parse("   [P]  "));
        Assert.Equal("", DfRichText.ToPlainText(null));
    }

    private static EmbarkSnapshot WithSheet(int unitId, int subTab, int tab, params string[] blocks) => new()
    {
        State = EmbarkScreenState.Embark,
        Sheet = new EmbarkOpenSheet(unitId, subTab, tab, blocks),
    };

    [Fact]
    public void Cache_keeps_personality_text_per_unit_and_tab()
    {
        var cache = new EmbarkSheetCache();

        Assert.True(cache.Update(WithSheet(11, EmbarkOpenSheet.PersonalitySubTab, 0, Mental, "")));
        Assert.False(cache.Update(WithSheet(11, EmbarkOpenSheet.PersonalitySubTab, 0, Mental, ""))); // igual: não muda
        Assert.True(cache.Update(WithSheet(11, EmbarkOpenSheet.PersonalitySubTab, 1, "She likes iron.")));
        Assert.Equal(2, cache.Version);

        Assert.Equal(2, cache.For(11).Count);
        Assert.Empty(cache.For(12));
        Assert.Empty(cache.For(null));
        Assert.Contains("She likes iron.", cache.PlainTextFor(11));
    }

    [Fact]
    public void Cache_ignores_other_tabs_and_empty_text()
    {
        var cache = new EmbarkSheetCache();

        Assert.False(cache.Update(WithSheet(11, 3, 0, Mental)));                                   // outra aba da ficha
        Assert.False(cache.Update(WithSheet(11, EmbarkOpenSheet.PersonalitySubTab, 0, "", " "))); // ainda sem texto
        Assert.False(cache.Update(new EmbarkSnapshot { State = EmbarkScreenState.Embark }));      // nenhuma ficha aberta
        Assert.Empty(cache.For(11));
    }
}
