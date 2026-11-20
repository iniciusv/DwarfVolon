using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwarfVolon.Core.Embark;
using DwarfVolon.Core.Markdown;
using DwarfVolon.DFHack;

namespace DwarfVolon.ViewModels;

/// <summary>
/// Tela do embarque: lê os anões da tela "Preparar com cuidado" pelo DFHack, analisa e mostra
/// a sugestão de funções e pontos em Markdown (o mesmo texto que vai para a IA).
/// </summary>
public partial class EmbarkViewModel : ViewModelBase
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(3);

    private readonly Func<CancellationToken, Task<EmbarkSnapshot>> _read;
    private readonly Func<string?> _connectionInfo;
    private DispatcherTimer? _timer;

    public EmbarkViewModel(DfHackSession session)
        : this(new EmbarkReader(session).ReadAsync, () => session.VersionText) { }

    public EmbarkViewModel(Func<CancellationToken, Task<EmbarkSnapshot>> read, Func<string?>? connectionInfo = null)
    {
        _read = read;
        _connectionInfo = connectionInfo ?? (() => null);
        Markdown = IntroMarkdown;
    }

    [ObservableProperty]
    public partial string Markdown { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Ainda não lido.";

    [ObservableProperty]
    public partial bool HasError { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial bool AutoRefresh { get; set; }

    /// <summary>Última análise (null se o jogo não estava na tela de embarque).</summary>
    public EmbarkAnalysis? Analysis { get; private set; }

    partial void OnAutoRefreshChanged(bool value)
    {
        if (value)
        {
            _timer ??= new DispatcherTimer(AutoRefreshInterval, DispatcherPriority.Background, async (_, _) =>
            {
                if (!IsBusy) await RefreshAsync();
            });
            _timer.Start();
        }
        else
        {
            _timer?.Stop();
        }
    }

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var snapshot = await _read(cts.Token);
            HasError = false;
            var connection = _connectionInfo();
            var prefix = connection is null ? "" : $"{connection} · ";

            if (snapshot.State != EmbarkScreenState.Embark)
            {
                Analysis = null;
                StatusText = $"{prefix}o jogo não está na tela de embarque";
                SetMarkdown(EmbarkMarkdown.NotOnEmbarkScreen(snapshot.Screen));
                return;
            }

            Analysis = EmbarkAnalyzer.Analyze(snapshot);
            StatusText = $"{prefix}{snapshot.Dwarves.Count} anões · atualizado às {DateTime.Now:HH:mm:ss}";
            SetMarkdown(EmbarkMarkdown.Build(Analysis));
        }
        catch (DfHackException ex)
        {
            Fail(ex.Message, NotConnectedMarkdown);
        }
        catch (EmbarkReadException ex)
        {
            Fail(ex.Message, null);
        }
        catch (OperationCanceledException)
        {
            Fail("O DFHack demorou demais para responder.", null);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Fail(string message, string? markdown)
    {
        HasError = true;
        Analysis = null;
        StatusText = message;
        if (markdown is not null) SetMarkdown(markdown);
    }

    /// <summary>Só troca o texto se mudou (evita redesenhar e perder a rolagem a cada atualização automática).</summary>
    private void SetMarkdown(string markdown)
    {
        if (markdown != Markdown) Markdown = markdown;
    }

    private static readonly string IntroMarkdown = new MarkdownBuilder()
        .Heading(1, "Embarque")
        .Paragraph("Analisa os anões da tela **Preparar com cuidado** do jogo e sugere quem faz o quê e onde gastar os pontos de habilidade.")
        .Numbered([
            "Abra o Dwarf Fortress com o **DFHack**.",
            "Escolha o local do embarque e clique em **Preparar com cuidado** (*Prepare carefully*).",
            "Clique em **Atualizar** aqui.",
        ])
        .Paragraph("Nada é alterado no jogo: o DwarfVolon só lê. As tabelas podem ser copiadas para uma IA.")
        .ToString();

    private static readonly string NotConnectedMarkdown = new MarkdownBuilder()
        .Heading(1, "Embarque")
        .Paragraph("Não consegui falar com o **DFHack**.")
        .Bullets([
            "Confira se o jogo está aberto e se o DFHack carregou (aparece o logo do DFHack na tela inicial).",
            "O DwarfVolon conecta em `127.0.0.1` na porta de `dfhack-config/remote-server.json` (padrão 5000).",
            "Antivírus ou firewall podem bloquear conexões locais.",
        ])
        .ToString();
}
