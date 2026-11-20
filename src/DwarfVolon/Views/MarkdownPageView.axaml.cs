using Avalonia.Controls;
using Avalonia.Interactivity;
using DwarfVolon.Services;
using DwarfVolon.ViewModels;

namespace DwarfVolon.Views;

public partial class MarkdownPageView : UserControl
{
    public MarkdownPageView()
    {
        InitializeComponent();
    }

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MarkdownPageViewModel vm && await ClipboardHelper.CopyAsync(this, vm.Markdown))
            CopiedText.Text = "Copiado!";
    }
}
