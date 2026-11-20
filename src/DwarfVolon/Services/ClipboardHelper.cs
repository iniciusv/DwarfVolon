using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace DwarfVolon.Services;

internal static class ClipboardHelper
{
    /// <summary>Copia texto para a área de transferência da janela que contém <paramref name="visual"/>.</summary>
    public static async Task<bool> CopyAsync(Visual visual, string? text)
    {
        if (string.IsNullOrEmpty(text) || TopLevel.GetTopLevel(visual)?.Clipboard is not { } clipboard) return false;
        await clipboard.SetTextAsync(text);
        return true;
    }
}
