using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwarfVolon.Core.DwarfFortress;
using DwarfVolon.Core.Settings;
using DwarfVolon.Services;

namespace DwarfVolon.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private AppSettings _settings = new();

    public SettingsViewModel(SettingsStore store)
    {
        _store = store;
    }

    public event Action<DwarfFortressInstallation?>? InstallationChanged;

    [ObservableProperty]
    public partial string DirectoryText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid), nameof(ValidityText), nameof(PortableText), nameof(UserDataText),
        nameof(WorldGenText), nameof(DfHackText), nameof(SummaryText))]
    public partial DwarfFortressInstallation? Installation { get; private set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    public bool IsValid => Installation?.IsValid == true;

    public string ValidityText => Installation switch
    {
        null => "Nenhuma pasta configurada",
        { IsValid: true } i => $"OK: {Path.GetFileName(i.ExecutablePath)} encontrado",
        _ => "Pasta inválida: não achei o executável e a pasta data/init",
    };

    public string PortableText => Installation is null ? "-" :
        Installation.IsPortable ? "Sim (prefs/portable.txt existe; dados ficam na pasta do jogo)" : "Não (dados ficam na pasta do usuário)";

    public string UserDataText => Installation?.UserDataDirectory ?? "-";

    public string WorldGenText => Installation is null ? "-" :
        $"{Installation.WorldGenFilePath} ({(File.Exists(Installation.WorldGenFilePath) ? "existe" : "ainda não existe; será criado ao salvar")})";

    public string DfHackText => Installation is null ? "-" : Installation.HasDfHack ? "Detectado" : "Não detectado";

    public string SummaryText => IsValid
        ? $"Dwarf Fortress: {Installation!.InstallDirectory}   •   world_gen.txt: {Installation.WorldGenFilePath}"
        : "Dwarf Fortress não configurado. Abra a aba Configurações.";

    public void Initialize()
    {
        _settings = _store.Load();
        if (!string.IsNullOrWhiteSpace(_settings.DwarfFortressDirectory))
        {
            DirectoryText = _settings.DwarfFortressDirectory;
            var configured = new DwarfFortressInstallation(_settings.DwarfFortressDirectory);
            if (configured.IsValid)
            {
                SetInstallation(configured);
                return;
            }
            StatusMessage = "A pasta salva não é mais válida; tentando detectar automaticamente...";
        }

        Detect();
    }

    [RelayCommand]
    private void Detect()
    {
        var found = DwarfFortressLocator.Detect();
        if (found is null)
        {
            StatusMessage = "Não encontrei o Dwarf Fortress nas bibliotecas da Steam. Use \"Procurar...\".";
            SetInstallation(string.IsNullOrWhiteSpace(DirectoryText) ? null : new DwarfFortressInstallation(DirectoryText));
            return;
        }

        DirectoryText = found.InstallDirectory;
        Persist(found);
        StatusMessage = "Detectado automaticamente e salvo.";
    }

    [RelayCommand]
    private void Apply() => UseDirectory(DirectoryText);

    public void UseDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            StatusMessage = "Informe uma pasta.";
            return;
        }

        DirectoryText = directory.Trim();
        var installation = new DwarfFortressInstallation(DirectoryText);
        if (!installation.IsValid)
        {
            SetInstallation(installation);
            StatusMessage = "Essa pasta não parece ser uma instalação do Dwarf Fortress.";
            return;
        }

        Persist(installation);
        StatusMessage = "Pasta salva.";
    }

    [RelayCommand]
    private void OpenInstallFolder()
    {
        if (Installation is not null) ShellHelper.OpenFolder(Installation.InstallDirectory);
    }

    [RelayCommand]
    private void OpenPrefsFolder()
    {
        if (Installation is not null) ShellHelper.OpenFolder(Installation.PrefsDirectory);
    }

    [RelayCommand]
    private void OpenAppDataFolder() => ShellHelper.OpenFolder(AppPaths.AppDataDirectory);

    private void Persist(DwarfFortressInstallation installation)
    {
        _settings.DwarfFortressDirectory = installation.InstallDirectory;
        _store.Save(_settings);
        SetInstallation(installation);
    }

    private void SetInstallation(DwarfFortressInstallation? installation)
    {
        Installation = installation;
        InstallationChanged?.Invoke(installation?.IsValid == true ? installation : null);
    }

    /// <summary>Atualiza os textos de status (ex.: depois que o world_gen.txt foi criado).</summary>
    public void Refresh() => OnPropertyChanged(nameof(WorldGenText));
}
