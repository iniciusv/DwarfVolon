using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using DwarfVolon.Core.Embark;

namespace DwarfVolon.Controls;

/// <summary>Mostra o texto de uma ficha do jogo com as cores do DF (verde = qualidade, vermelho = defeito).</summary>
public sealed class DfRichTextView : StackPanel
{
    public static readonly StyledProperty<IReadOnlyList<DfTextParagraph>?> ParagraphsProperty =
        AvaloniaProperty.Register<DfRichTextView, IReadOnlyList<DfTextParagraph>?>(nameof(Paragraphs));

    static DfRichTextView()
    {
        ParagraphsProperty.Changed.AddClassHandler<DfRichTextView>((view, _) => view.Build());
    }

    public DfRichTextView()
    {
        Spacing = 8;
        ActualThemeVariantChanged += (_, _) => Build();
    }

    public IReadOnlyList<DfTextParagraph>? Paragraphs
    {
        get => GetValue(ParagraphsProperty);
        set => SetValue(ParagraphsProperty, value);
    }

    private void Build()
    {
        Children.Clear();
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var positive = new SolidColorBrush(Color.Parse(dark ? "#0ca30c" : "#006300"));
        var negative = new SolidColorBrush(Color.Parse(dark ? "#e66767" : "#b3261e"));

        foreach (var paragraph in Paragraphs ?? [])
        {
            var block = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
            foreach (var run in paragraph.Runs)
            {
                var inline = new Run(run.Text);
                if (run.Tone == DfTextTone.Positive) inline.Foreground = positive;
                else if (run.Tone == DfTextTone.Negative) inline.Foreground = negative;
                block.Inlines!.Add(inline);
            }
            Children.Add(block);
        }
    }
}
