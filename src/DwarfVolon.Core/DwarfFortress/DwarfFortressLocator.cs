using System.Text.RegularExpressions;

namespace DwarfVolon.Core.DwarfFortress;

/// <summary>Procura o Dwarf Fortress nas bibliotecas da Steam.</summary>
public static partial class DwarfFortressLocator
{
    public const string SteamFolderName = "Dwarf Fortress";

    public static DwarfFortressInstallation? Detect() =>
        CandidateDirectories()
            .Select(dir => new DwarfFortressInstallation(dir))
            .FirstOrDefault(inst => inst.IsValid);

    public static IEnumerable<string> CandidateDirectories() =>
        FindSteamLibraries()
            .Select(lib => Path.Combine(lib, "steamapps", "common", SteamFolderName))
            .Where(Directory.Exists);

    public static IReadOnlyList<string> FindSteamLibraries()
    {
        var libraries = new List<string>();
        foreach (var steamRoot in SteamRoots().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(steamRoot)) continue;
            libraries.Add(steamRoot);
            var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf)) libraries.AddRange(ParseLibraryFolders(File.ReadAllText(vdf)));
        }

        return libraries
            .Select(p => p.TrimEnd('\\', '/'))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Extrai os caminhos <c>"path"</c> do libraryfolders.vdf da Steam.</summary>
    public static IReadOnlyList<string> ParseLibraryFolders(string vdfContent) =>
        VdfPathRegex().Matches(vdfContent)
            .Select(m => m.Groups[1].Value.Replace(@"\\", @"\"))
            .ToList();

    private static IEnumerable<string> SteamRoots()
    {
        if (OperatingSystem.IsWindows())
        {
            if (ReadSteamPathFromRegistry() is { } fromRegistry) yield return fromRegistry.Replace('/', '\\');
            yield return @"C:\Program Files (x86)\Steam";
            yield return @"C:\Program Files\Steam";
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(home, ".steam", "steam");
            yield return Path.Combine(home, ".local", "share", "Steam");
            yield return Path.Combine(home, "Library", "Application Support", "Steam");
        }
    }

    private static string? ReadSteamPathFromRegistry()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex VdfPathRegex();
}
