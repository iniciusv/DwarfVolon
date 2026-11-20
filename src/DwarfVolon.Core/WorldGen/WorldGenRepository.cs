namespace DwarfVolon.Core.WorldGen;

public sealed record WorldGenSaveResult(string FilePath, bool Replaced, string? BackupPath);

/// <summary>Lê e grava o prefs/world_gen.txt do jogo, sempre com backup antes de alterar.</summary>
public sealed class WorldGenRepository(string worldGenFilePath)
{
    public const string BackupFolderName = "dwarfvolon-backups";

    public string FilePath { get; } = worldGenFilePath;

    public bool Exists => File.Exists(FilePath);

    public string BackupDirectory => Path.Combine(Path.GetDirectoryName(FilePath)!, BackupFolderName);

    public WorldGenDocument Load() => Exists ? WorldGenDocument.Load(FilePath) : WorldGenDocument.CreateEmpty();

    /// <summary>Insere ou substitui (pelo título) o conjunto no arquivo.</summary>
    public WorldGenSaveResult Save(WorldGenParameterSet set)
    {
        var document = Load();
        var replaced = document.Upsert(set);
        var backup = Write(document);
        return new WorldGenSaveResult(FilePath, replaced, backup);
    }

    public WorldGenSaveResult Remove(string title)
    {
        var document = Load();
        if (!document.RemoveByTitle(title)) return new WorldGenSaveResult(FilePath, false, null);
        var backup = Write(document);
        return new WorldGenSaveResult(FilePath, true, backup);
    }

    private string? Write(WorldGenDocument document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string? backup = null;
        if (Exists)
        {
            Directory.CreateDirectory(BackupDirectory);
            backup = Path.Combine(BackupDirectory, $"world_gen.{DateTime.Now:yyyyMMdd-HHmmss-fff}.txt");
            File.Copy(FilePath, backup, overwrite: false);
        }

        // Grava num temporário e troca, para não deixar o arquivo pela metade se algo falhar.
        var temp = FilePath + ".tmp";
        document.Save(temp);
        File.Move(temp, FilePath, overwrite: true);
        return backup;
    }
}
