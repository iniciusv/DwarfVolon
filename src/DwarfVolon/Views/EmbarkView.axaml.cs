using Avalonia.Controls;
using Avalonia.Interactivity;
using DwarfVolon.Services;
using DwarfVolon.ViewModels;

namespace DwarfVolon.Views;

public partial class EmbarkView : UserControl
{
    public EmbarkView()
    {
        InitializeComponent();
    }

    private EmbarkViewModel? Vm => DataContext as EmbarkViewModel;

    private async void CopySummary_Click(object? sender, RoutedEventArgs e) =>
        await CopyAsync(Vm?.SummaryForAi, "Resumo copiado.");

    private async void CopyRequest_Click(object? sender, RoutedEventArgs e) =>
        await CopyAsync(Vm?.PointsRequestForAi(), "Pedido copiado.");

    private async void CopyPlan_Click(object? sender, RoutedEventArgs e) =>
        await CopyAsync(Vm?.PlanForAi(), "Plano copiado.");

    private async void PasteResponse_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.ImportAiResponse(await ClipboardHelper.GetTextAsync(this));
    }

    private async Task CopyAsync(string? text, string done)
    {
        CopiedText.Text = await ClipboardHelper.CopyAsync(this, text) ? done : "Nada para copiar ainda.";
    }
}
