using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using DwarfVolon.Core.Markdown;

namespace DwarfVolon.Controls;

/// <summary>
/// Renderiza Markdown (o subconjunto do <see cref="MarkdownParser"/>) como controles Avalonia.
/// Usado para mostrar na tela o mesmo texto que pode ser copiado e enviado para a IA.
/// </summary>
public sealed class MarkdownView : UserControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    private static readonly FontFamily Mono = new("Cascadia Mono,Consolas,Menlo,monospace");
    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#E0A030"));
    private static readonly IBrush Subtle = new SolidColorBrush(Color.Parse("#40808080"));
    private static readonly IBrush HeaderFill = new SolidColorBrush(Color.Parse("#26808080"));
    private static readonly IBrush StripeFill = new SolidColorBrush(Color.Parse("#0D808080"));
    private static readonly IBrush CodeFill = new SolidColorBrush(Color.Parse("#22808080"));
    private static readonly IBrush QuoteFill = new SolidColorBrush(Color.Parse("#14E0A030"));

    static MarkdownView()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownView>((view, _) => view.Render());
    }

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Largura máxima das células de tabela antes de quebrar linha.</summary>
    public double MaxCellWidth { get; set; } = 420;

    private void Render()
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var block in MarkdownParser.Parse(Markdown ?? ""))
        {
            Control control = block switch
            {
                MdHeading h => Heading(h, isFirst: panel.Children.Count == 0),
                MdParagraph p => Text(p.Text),
                MdList l => List(l),
                MdQuote q => new Border
                {
                    BorderBrush = Accent, BorderThickness = new Thickness(3, 0, 0, 0), Background = QuoteFill,
                    Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(4), Child = Text(q.Text),
                },
                MdCode c => new Border
                {
                    Background = CodeFill, CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 8),
                    Child = new SelectableTextBlock { Text = c.Text, FontFamily = Mono, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                },
                MdRule => new Border { Height = 1, Background = Subtle, Margin = new Thickness(0, 4) },
                MdTable t => Table(t),
                _ => new TextBlock(),
            };
            panel.Children.Add(control);
        }
        Content = panel;
    }

    private static Control Heading(MdHeading heading, bool isFirst)
    {
        var block = Text(heading.Text);
        block.FontSize = heading.Level switch { 1 => 22, 2 => 17, 3 => 15, _ => 14 };
        block.FontWeight = FontWeight.SemiBold;
        if (!isFirst && heading.Level <= 2) block.Margin = new Thickness(0, 10, 0, 0);
        return block;
    }

    private static SelectableTextBlock Text(string markdownInline)
    {
        var block = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, Inlines = [] };
        foreach (var inline in MarkdownParser.ParseInlines(markdownInline))
        {
            var run = new Run(inline.Text);
            if (inline.Bold) run.FontWeight = FontWeight.SemiBold;
            if (inline.Italic) run.FontStyle = FontStyle.Italic;
            if (inline.Code)
            {
                run.FontFamily = Mono;
                run.Background = CodeFill;
            }
            block.Inlines!.Add(run);
        }
        return block;
    }

    private static Control List(MdList list)
    {
        var panel = new StackPanel { Spacing = 4 };
        for (var i = 0; i < list.Items.Count; i++)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var marker = new TextBlock { Text = list.Ordered ? $"{i + 1}." : "•", Margin = new Thickness(4, 0, 8, 0), Opacity = 0.8 };
            var text = Text(list.Items[i]);
            Grid.SetColumn(text, 1);
            row.Children.Add(marker);
            row.Children.Add(text);
            panel.Children.Add(row);
        }
        return panel;
    }

    private Control Table(MdTable table)
    {
        var grid = new Grid();
        for (var c = 0; c < table.Headers.Count; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r <= table.Rows.Count; r++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var c = 0; c < table.Headers.Count; c++)
        {
            var header = Cell(table.Headers[c], table.Aligns[c], HeaderFill, bold: true);
            Grid.SetColumn(header, c);
            grid.Children.Add(header);
        }

        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < table.Headers.Count; c++)
            {
                var cell = Cell(table.Rows[r][c], table.Aligns[c], r % 2 == 1 ? StripeFill : null, bold: false);
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new Border
            {
                BorderBrush = Subtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = true, Child = grid,
            },
        };
    }

    private Border Cell(string markdownInline, MdAlign align, IBrush? background, bool bold)
    {
        var text = Text(markdownInline);
        text.MaxWidth = MaxCellWidth;
        text.FontSize = 13;
        if (bold) text.FontWeight = FontWeight.SemiBold;
        text.HorizontalAlignment = align switch
        {
            MdAlign.Right => HorizontalAlignment.Right,
            MdAlign.Center => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Left,
        };
        return new Border
        {
            Background = background, Padding = new Thickness(10, 5), Child = text,
            BorderBrush = Subtle, BorderThickness = new Thickness(0, 0, 0, bold ? 1 : 0),
        };
    }
}
