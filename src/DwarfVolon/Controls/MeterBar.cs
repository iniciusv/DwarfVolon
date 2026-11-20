using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace DwarfVolon.Controls;

/// <summary>
/// Barra fina para leitura rápida. Dois modos:
/// <list type="bullet">
/// <item>Magnitude (<see cref="Origin"/> nulo): cresce do mínimo, em azul. <see cref="Reference"/> desenha um traço (ex.: mediana).</item>
/// <item>Divergente (<see cref="Origin"/> definido): cresce a partir do neutro; azul acima, vermelho abaixo.</item>
/// </list>
/// Cores da paleta de referência (azul/vermelho com trilho cinza neutro), com variantes para tema escuro.
/// </summary>
public sealed class MeterBar : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<MeterBar, double>(nameof(Value));

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<MeterBar, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<MeterBar, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double?> OriginProperty =
        AvaloniaProperty.Register<MeterBar, double?>(nameof(Origin));

    public static readonly StyledProperty<double?> ReferenceProperty =
        AvaloniaProperty.Register<MeterBar, double?>(nameof(Reference));

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<MeterBar, bool>(nameof(IsMuted));

    private const double Thickness = 8;
    private const double Radius = 4;

    static MeterBar()
    {
        AffectsRender<MeterBar>(ValueProperty, MinimumProperty, MaximumProperty, OriginProperty, ReferenceProperty, IsMutedProperty);
    }

    public MeterBar()
    {
        Height = 16;
        MinWidth = 80;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Ponto neutro de onde a barra sai (modo divergente). Nulo = barra de magnitude.</summary>
    public double? Origin
    {
        get => GetValue(OriginProperty);
        set => SetValue(OriginProperty, value);
    }

    /// <summary>Traço de referência (ex.: 50 = mediana) no modo magnitude.</summary>
    public double? Reference
    {
        get => GetValue(ReferenceProperty);
        set => SetValue(ReferenceProperty, value);
    }

    /// <summary>Desenha a barra com menos destaque (ex.: funções que não foram sugeridas).</summary>
    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var track = new SolidColorBrush(Color.Parse(dark ? "#383835" : "#f0efec"));
        var baseline = new SolidColorBrush(Color.Parse(dark ? "#898781" : "#898781"));
        var above = Color.Parse(dark ? "#3987e5" : "#2a78d6");
        var below = Color.Parse(dark ? "#e66767" : "#e34948");

        var width = Bounds.Width;
        var top = (Bounds.Height - Thickness) / 2;
        var span = Maximum - Minimum;
        if (width <= 0 || span <= 0) return;
        double X(double v) => Math.Clamp((v - Minimum) / span, 0, 1) * width;

        context.DrawRectangle(track, null, new RoundedRect(new Rect(0, top, width, Thickness), Radius));

        var value = Math.Clamp(Value, Minimum, Maximum);
        var origin = Origin ?? Minimum;
        var x0 = X(origin);
        var x1 = X(value);
        var color = value >= origin ? above : below;
        var fill = new SolidColorBrush(color, IsMuted ? 0.45 : 1);

        if (Math.Abs(x1 - x0) >= 0.5)
        {
            var left = Math.Min(x0, x1);
            var right = Math.Max(x0, x1);
            // Ponta arredondada só no lado do valor; quadrada no lado da origem.
            context.DrawRectangle(fill, null, new RoundedRect(new Rect(left, top, right - left, Thickness), Radius));
            var squareSide = x1 >= x0
                ? new Rect(left, top, Math.Min(Radius, right - left), Thickness)
                : new Rect(Math.Max(left, right - Radius), top, Math.Min(Radius, right - left), Thickness);
            context.DrawRectangle(fill, null, squareSide);
        }

        var tick = Origin ?? Reference;
        if (tick is { } t)
        {
            var x = Math.Round(X(t)) + 0.5;
            context.DrawLine(new Pen(baseline, 1), new Point(x, top - 3), new Point(x, top + Thickness + 3));
        }
    }
}
