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

    private async void Copy_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EmbarkViewModel vm && await ClipboardHelper.CopyAsync(this, vm.Markdown))
            CopiedText.Text = "Copiado!";
    }
}
