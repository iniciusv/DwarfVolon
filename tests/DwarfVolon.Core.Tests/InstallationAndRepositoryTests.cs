using DwarfVolon.Core.DwarfFortress;
using DwarfVolon.Core.WorldGen;

namespace DwarfVolon.Core.Tests;

public sealed class InstallationAndRepositoryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("dwarfvolon-tests").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateFakeInstall(bool portable)
    {
        var install = Path.Combine(_root, "Dwarf Fortress");
        Directory.CreateDirectory(Path.Combine(install, "data", "init"));
        File.WriteAllText(Path.Combine(install, DwarfFortressInstallation.WindowsExecutable), "");
        if (portable)
        {
            Directory.CreateDirectory(Path.Combine(install, "prefs"));
            File.WriteAllText(Path.Combine(install, "prefs", "portable.txt"), "");
        }
        return install;
    }

    [Fact]
    public void Instalacao_NaoPortatil_UsaPastaDoUsuario()
    {
        var userData = Path.Combine(_root, "appdata");
        var inst = new DwarfFortressInstallation(CreateFakeInstall(portable: false), userData);

        Assert.True(inst.IsValid);
        Assert.False(inst.IsPortable);
        Assert.Equal(Path.Combine(userData, "prefs", "world_gen.txt"), inst.WorldGenFilePath);
    }

    [Fact]
    public void Instalacao_Portatil_UsaPastaDoJogo()
    {
        var install = CreateFakeInstall(portable: true);
        var inst = new DwarfFortressInstallation(install, Path.Combine(_root, "appdata"));

        Assert.True(inst.IsPortable);
        Assert.Equal(Path.Combine(install, "prefs", "world_gen.txt"), inst.WorldGenFilePath);
    }

    [Fact]
    public void Instalacao_SemExecutavel_EhInvalida()
    {
        Assert.False(new DwarfFortressInstallation(_root).IsValid);
    }

    [Fact]
    public void Locator_LeLibraryFoldersDaSteam()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0" { "path"		"C:\\Program Files (x86)\\Steam" "label" "" }
                "1" { "path"		"V:\\SteamLibrary" }
            }
            """;
        Assert.Equal([@"C:\Program Files (x86)\Steam", @"V:\SteamLibrary"], DwarfFortressLocator.ParseLibraryFolders(vdf));
    }

    [Fact]
    public void Repositorio_CriaArquivoEDepoisFazBackup()
    {
        var path = Path.Combine(_root, "prefs", "world_gen.txt");
        var repo = new WorldGenRepository(path);
        var set = BuiltInTemplates.Load().FindByTitle("SMALL REGION")!.Clone();
        set.Title = "MEU MUNDO";

        var first = repo.Save(set);
        Assert.True(File.Exists(path));
        Assert.False(first.Replaced);
        Assert.Null(first.BackupPath);

        set.Set("END_YEAR", "125");
        var second = repo.Save(set);
        Assert.True(second.Replaced);
        Assert.True(File.Exists(second.BackupPath));

        var doc = repo.Load();
        Assert.Single(doc.ParameterSets);
        Assert.Equal(["125"], doc.FindByTitle("MEU MUNDO")!.Find("END_YEAR")!.Values);
    }

    [Fact]
    public void Repositorio_PreservaConjuntosDoUsuario()
    {
        var path = Path.Combine(_root, "world_gen.txt");
        File.WriteAllText(path, "[WORLD_GEN]\n\t[TITLE:DO USUARIO]\n\t[DIM:17:17]\n");
        var repo = new WorldGenRepository(path);

        repo.Save(WorldGenParameterSet.Create("NOVO"));

        Assert.Equal("[WORLD_GEN]\n\t[TITLE:DO USUARIO]\n\t[DIM:17:17]\n\n[WORLD_GEN]\n\t[TITLE:NOVO]\n", File.ReadAllText(path));
    }
}
