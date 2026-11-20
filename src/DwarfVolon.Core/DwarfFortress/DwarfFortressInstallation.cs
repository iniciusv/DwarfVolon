namespace DwarfVolon.Core.DwarfFortress;

/// <summary>
/// Caminhos de uma instalação do Dwarf Fortress (v50+ / Steam).
///
/// Regra do jogo para a pasta de dados do usuário (prefs, save, mods):
/// se existir <c>&lt;instalação&gt;/prefs/portable.txt</c>, o jogo está em modo portátil e usa a
/// própria pasta de instalação; senão usa <c>%APPDATA%/Bay 12 Games/Dwarf Fortress</c>
/// (no Linux, <c>~/.local/share/Bay 12 Games/Dwarf Fortress</c>).
/// </summary>
public sealed class DwarfFortressInstallation
{
    public const string WindowsExecutable = "Dwarf Fortress.exe";
    public const string LinuxExecutable = "dwarfort";

    private readonly string? _userDataOverride;

    /// <param name="installDirectory">Pasta onde está o executável do jogo.</param>
    /// <param name="userDataDirectoryOverride">Para testes: substitui a pasta de dados do usuário fora do modo portátil.</param>
    public DwarfFortressInstallation(string installDirectory, string? userDataDirectoryOverride = null)
    {
        InstallDirectory = Path.GetFullPath(installDirectory);
        _userDataOverride = userDataDirectoryOverride;
    }

    public string InstallDirectory { get; }

    public string? ExecutablePath =>
        new[] { WindowsExecutable, LinuxExecutable }
            .Select(name => Path.Combine(InstallDirectory, name))
            .FirstOrDefault(File.Exists);

    /// <summary>Tem executável e a pasta data/init: é uma instalação válida.</summary>
    public bool IsValid => ExecutablePath is not null && Directory.Exists(Path.Combine(InstallDirectory, "data", "init"));

    public bool IsPortable => File.Exists(Path.Combine(InstallDirectory, "prefs", "portable.txt"));

    public string UserDataDirectory => IsPortable ? InstallDirectory : (_userDataOverride ?? DefaultUserDataDirectory);

    public string PrefsDirectory => Path.Combine(UserDataDirectory, "prefs");

    public string SaveDirectory => Path.Combine(UserDataDirectory, "save");

    public string WorldGenFilePath => Path.Combine(PrefsDirectory, "world_gen.txt");

    /// <summary>DFHack instalado (via Steam ele coloca dfhooks_dfhack.ini e dfhack-config na pasta do jogo).</summary>
    public bool HasDfHack =>
        File.Exists(Path.Combine(InstallDirectory, "dfhooks_dfhack.ini")) ||
        Directory.Exists(Path.Combine(InstallDirectory, "hack")) ||
        Directory.Exists(Path.Combine(InstallDirectory, "dfhack-config"));

    public static string DefaultUserDataDirectory
    {
        get
        {
            var root = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Bay 12 Games", "Dwarf Fortress");
        }
    }
}
