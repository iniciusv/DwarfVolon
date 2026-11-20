using System.Text.Json;
using DwarfVolon.Core.WorldGen;

namespace DwarfVolon.Core.Settings;

public sealed class AppSettings
{
    /// <summary>Pasta de instalação do Dwarf Fortress (onde está o executável).</summary>
    public string? DwarfFortressDirectory { get; set; }
}

/// <summary>Pastas do próprio DwarfVolon (configurações e presets do usuário).</summary>
public static class AppPaths
{
    public static string AppDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DwarfVolon");

    public static string SettingsFile => Path.Combine(AppDataDirectory, "settings.json");

    public static string UserModifiersDirectory => Path.Combine(AppDataDirectory, "modificadores");
}

public sealed class SettingsStore(string filePath)
{
    public SettingsStore() : this(AppPaths.SettingsFile) { }

    public string FilePath { get; } = filePath;

    public AppSettings Load()
    {
        if (!File.Exists(FilePath)) return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), WorldGenJson.Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, WorldGenJson.Options));
    }
}
