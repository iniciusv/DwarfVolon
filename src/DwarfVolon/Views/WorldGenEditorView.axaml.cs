using Avalonia.Controls;
using Avalonia.Interactivity;
using DwarfVolon.Services;
using DwarfVolon.ViewModels;

namespace DwarfVolon.Views;

public partial class WorldGenEditorView : UserControl
{
    public WorldGenEditorView()
    {
        InitializeComponent();
    }

    private WorldGenEditorViewModel? ViewModel => DataContext as WorldGenEditorViewModel;

    private async void CopySummary_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && await ClipboardHelper.CopyAsync(this, vm.MarkdownSummary))
            ShowCopied("Resumo copiado em Markdown.");
    }

    private async void CopyCatalog_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && await ClipboardHelper.CopyAsync(this, vm.MarkdownCatalog))
            ShowCopied("Catálogo copiado em Markdown.");
    }

    private void ShowCopied(string message)
    {
        CopiedText.Text = message;
        CopiedAdvancedText.Text = message;
    }
}
