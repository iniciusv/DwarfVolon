using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DwarfVolon.Core.Markdown;
using DwarfVolon.Core.Settings;
using DwarfVolon.DFHack;

namespace DwarfVolon.ViewModels;

/// <summary>Item do menu lateral. Cada página é um view-model; a view vem dos DataTemplates da janela.</summary>
public sealed record NavItemViewModel(string Icon, string Title, ViewModelBase Page, string? Badge = null)
{
    public bool HasBadge => Badge is not null;
}

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel() : this(new SettingsStore()) { }

    public MainWindowViewModel(SettingsStore store)
    {
        Settings = new SettingsViewModel(store);
        WorldGen = new WorldGenEditorViewModel();
        DfHack = new DfHackSession(() => DfHackEndpoint.Resolve(Settings.Installation));
        Embark = new EmbarkViewModel(DfHack);

        Settings.InstallationChanged += installation => WorldGen.Installation = installation;
        WorldGen.Saved += Settings.Refresh;
        Settings.Initialize();

        var worldGenItem = new NavItemViewModel("🌍", "Gerador de mundo", WorldGen);
        var settingsItem = new NavItemViewModel("⚙️", "Configurações", Settings);
        NavItems =
        [
            worldGenItem,
            new NavItemViewModel("🎒", "Embarque", Embark),
            new NavItemViewModel("⛏️", "Anões", new MarkdownPageViewModel("Anões", new MarkdownBuilder()
                .Heading(1, "Anões")
                .Paragraph("Visão geral da fortaleza pelo **DFHack**, com o jogo aberto. Em breve.")
                .Heading(2, "Planejado")
                .Bullets([
                    "Tabela de anões com as colunas que você escolher (profissão, habilidades, humor, ferimentos...)",
                    "Filtros como *ociosos* e *com habilidade X sem usar*",
                    "Atribuir funções e work details com poucos cliques",
                ])
                .Heading(2, "Exemplo de tabela")
                .Paragraph("As tabelas são Markdown: a mesma que aparece aqui pode ser copiada e enviada para a IA.")
                .Table(["Nome", "Profissão", "Mineração", "Humor", "Ocioso"],
                [
                    ["Urist", "Minerador", 12, "Contente", false],
                    ["Bomrek", "Pedreiro", 3, "Estressado", true],
                    ["Kadol", "Ferreiro", 0, "Feliz", false],
                ], [MdAlign.Left, MdAlign.Left, MdAlign.Right, MdAlign.Left, MdAlign.Center])
                .ToString()), "em breve"),
            new NavItemViewModel("💬", "Assistente", new MarkdownPageViewModel("Assistente", new MarkdownBuilder()
                .Heading(1, "Assistente de IA")
                .Paragraph("Chat com **DeepSeek** que consulta a fortaleza e sugere ou executa ações, sempre com a sua aprovação. Em breve.")
                .Heading(2, "Planejado")
                .Bullets([
                    "Perguntas sobre a fortaleza (\"quem está ocioso?\", \"tem comida para o inverno?\")",
                    "Contexto enviado como **tabelas Markdown** compactas, geradas pelos mesmos módulos das telas",
                    "Ações de escrita só depois de um cartão de aprovação",
                ])
                .Quote("Dica: no Gerador de mundo, o botão \"Copiar Markdown\" já gera um resumo pronto para colar numa IA.")
                .ToString()), "em breve"),
            settingsItem,
        ];

        SelectedNav = Settings.IsValid ? worldGenItem : settingsItem;
    }

    public SettingsViewModel Settings { get; }

    public WorldGenEditorViewModel WorldGen { get; }

    public DfHackSession DfHack { get; }

    public EmbarkViewModel Embark { get; }

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    [ObservableProperty]
    public partial NavItemViewModel SelectedNav { get; set; }

    partial void OnSelectedNavChanged(NavItemViewModel oldValue, NavItemViewModel newValue)
    {
        // Evita ficar sem página se a lista perder a seleção.
        if (newValue is null && oldValue is not null) SelectedNav = oldValue;

        // Páginas que leem o jogo atualizam ao serem abertas.
        if (newValue?.Page is EmbarkViewModel embark && embark.RefreshCommand.CanExecute(null))
            embark.RefreshCommand.Execute(null);
    }
}

/// <summary>Página cujo conteúdo é um documento Markdown (renderizado e copiável).</summary>
public sealed class MarkdownPageViewModel(string title, string markdown) : ViewModelBase
{
    public string Title { get; } = title;
    public string Markdown { get; } = markdown;
}
