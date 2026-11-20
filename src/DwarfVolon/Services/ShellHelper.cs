using System.Diagnostics;

namespace DwarfVolon.Services;

internal static class ShellHelper
{
    /// <summary>Abre a pasta no gerenciador de arquivos do sistema (criando se não existir).</summary>
    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
}
