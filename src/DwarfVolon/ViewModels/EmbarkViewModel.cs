using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DwarfVolon.Core.Embark;
using DwarfVolon.Core.Markdown;
using DwarfVolon.DFHack;

namespace DwarfVolon.ViewModels;

/// <summary>
/// Tela do embarque: lê os anões da tela "Preparar com cuidado" pelo DFHack, mostra a ficha de cada um
/// e guarda as decisões (funções e plano de pontos), que podem ser aplicadas no jogo e ficam salvas
/// por mundo para depois do embarque.
/// </summary>
public partial class EmbarkViewModel : ViewModelBase
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(3);

    private readonly Func<CancellationToken, Task<EmbarkSnapshot>> _read;
    private readonly Func<string?> _connectionInfo;
    private readonly Func<IReadOnlyList<EmbarkApplyItem>, CancellationToken, Task<EmbarkApplyResult>>? _apply;
    private readonly string? _planDirectory;
    private DispatcherTimer? _timer;
    private string? _lastSignature;
    private string? _planPath;
    private bool _restoring;

    /// <summary>Texto das fichas abertas no jogo, guardado por anão durante a sessão.</summary>
    public EmbarkSheetCache Sheets { get; } = new();

    /// <summary>Fichas dos anões (subaba Anões).</summary>
    public ObservableCollection<DwarfSheetViewModel> Dwarves { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDwarf))]
    public partial DwarfSheetViewModel? SelectedDwarf { get; set; }

    public bool HasSelectedDwarf => SelectedDwarf is not null;

    public bool HasDwarves => Dwarves.Count > 0;

    public EmbarkViewModel(DfHackSession session)
        : this(new EmbarkReader(session).ReadAsync, () => session.VersionText, new EmbarkWriter(session).ApplyAsync,
            EmbarkPlanStore.DirectoryPath) { }

    /// <param name="apply">Aplica planos no jogo. Nulo = só planeja.</param>
    /// <param name="planDirectory">Pasta onde as decisões são salvas por mundo. Nulo = não salva (testes).</param>
    public EmbarkViewModel(Func<CancellationToken, Task<EmbarkSnapshot>> read, Func<string?>? connectionInfo = null,
        Func<IReadOnlyList<EmbarkApplyItem>, CancellationToken, Task<EmbarkApplyResult>>? apply = null,
        string? planDirectory = null)
    {
        _read = read;
        _connectionInfo = connectionInfo ?? (() => null);
        _apply = apply;
        _planDirectory = planDirectory;
        Markdown = IntroMarkdown;
        Plan.Changed += OnPlanChanged;
    }

    /// <summary>Onde as decisões estão salvas (ou de onde foram restauradas).</summary>
    [ObservableProperty]
    public partial string PlanFileText { get; private set; } = "";

    /// <summary>Confirmação de "aplicar todos": o que vai mudar no jogo.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmingApplyAll))]
    [NotifyCanExecuteChangedFor(nameof(ApplyAllCommand), nameof(ConfirmApplyAllCommand))]
    public partial string PendingApplyAll { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasApplyAllMessage))]
    public partial string ApplyAllMessage { get; private set; } = "";

    [ObservableProperty]
    public partial bool ApplyAllFailed { get; private set; }

    public bool IsConfirmingApplyAll => PendingApplyAll.Length > 0;
    public bool HasApplyAllMessage => ApplyAllMessage.Length > 0;
    public bool CanWriteToGame => _apply is not null;

    /// <summary>Preenche as funções de todos os anões com a sugestão do app (pela análise de atributos).</summary>
    [RelayCommand]
    private void SuggestRolesForAll()
    {
        if (Analysis is null) return;
        foreach (var dwarf in Analysis.Snapshot.Dwarves)
            Plan.SetRoles(dwarf, Analysis.AssignmentsFor(dwarf).Select(a => a.Role.Id).ToList(), PlanSource.AppSuggestion);
    }

    /// <summary>Anões com plano de pontos diferente do jogo e dentro das regras.</summary>
    private List<EmbarkDwarf> DwarvesToApply() => Analysis is not { } analysis
        ? []
        : analysis.Snapshot.Dwarves
            .Where(Plan.Has)
            .Where(d => EmbarkPlanRules.Diff(analysis.Snapshot, d, Plan.For(d)).Count > 0)
            .Where(d => EmbarkPlanRules.Check(analysis.Snapshot, d, Plan.For(d)).Count == 0)
            .ToList();

    private bool CanApplyAll() => _apply is not null && !IsConfirmingApplyAll;

    /// <summary>Mostra o que vai mudar no jogo para todos os anões com plano e pede confirmação.</summary>
    [RelayCommand(CanExecute = nameof(CanApplyAll))]
    private void ApplyAll()
    {
        ApplyAllMessage = "";
        var dwarves = DwarvesToApply();
        if (Analysis is not { } analysis || dwarves.Count == 0)
        {
            ApplyAllFailed = false;
            ApplyAllMessage = "Nada para aplicar: os planos já estão iguais ao jogo (ou têm problemas a corrigir na ficha).";
            return;
        }
        PendingApplyAll = string.Join(Environment.NewLine, dwarves.Select(d =>
            $"{d.DisplayName}: {string.Join(", ", EmbarkPlanRules.Diff(analysis.Snapshot, d, Plan.For(d)))}"));
    }

    private bool CanConfirmApplyAll() => IsConfirmingApplyAll;

    [RelayCommand(CanExecute = nameof(CanConfirmApplyAll))]
    private async Task ConfirmApplyAllAsync()
    {
        var dwarves = DwarvesToApply();
        PendingApplyAll = "";
        var error = await ApplyToGameAsync(dwarves);
        ApplyAllFailed = error is not null;
        ApplyAllMessage = error ?? $"{dwarves.Count} anões aplicados no jogo às {DateTime.Now:HH:mm:ss}.";
    }

    [RelayCommand]
    private void CancelApplyAll() => PendingApplyAll = "";

    /// <summary>Escreve no jogo o plano de pontos dos anões e relê a tela. Devolve o erro ou nulo.</summary>
    public async Task<string?> ApplyToGameAsync(IReadOnlyList<EmbarkDwarf> dwarves)
    {
        if (_apply is null) return "Sem conexão com o jogo.";
        if (dwarves.Count == 0) return null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await _apply(dwarves.Select(d => EmbarkApplyItem.From(d, Plan)).ToList(), cts.Token);
            if (!result.Ok) return $"O jogo não aceitou: {result.Error}";
            await RefreshAsync();
            return null;
        }
        catch (Exception ex) when (ex is DfHackException or EmbarkReadException or OperationCanceledException)
        {
            return ex.Message;
        }
    }

    private void OnPlanChanged(EmbarkDwarf dwarf)
    {
        if (_restoring || Analysis is null) return;
        SetMarkdown(EmbarkMarkdown.Build(Analysis, Plan));
        SetSupplies(EmbarkSupplies.Build(Analysis, Plan));
        if (_planPath is null) return;
        try
        {
            EmbarkPlanStore.Save(_planPath, Analysis.Snapshot, Plan);
            PlanFileText = $"Decisões salvas às {DateTime.Now:HH:mm:ss} (mundo {Analysis.Snapshot.World}).";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PlanFileText = $"Não consegui salvar as decisões: {ex.Message}";
        }
    }

    /// <summary>Na primeira leitura de um mundo, restaura as decisões salvas dele.</summary>
    private void LoadSavedPlan(EmbarkSnapshot snapshot)
    {
        var path = _planDirectory is null ? null : EmbarkPlanStore.PathFor(snapshot, _planDirectory);
        if (path == _planPath) return;
        _planPath = path;
        if (path is null) return;

        _restoring = true;
        try
        {
            var restored = EmbarkPlanStore.Load(path, snapshot, Plan);
            PlanFileText = restored > 0
                ? $"Decisões de {restored} anões restauradas (mundo {snapshot.World})."
                : $"As decisões serão salvas para o mundo {snapshot.World}.";
        }
        finally
        {
            _restoring = false;
        }
    }

    [ObservableProperty]
    public partial string Markdown { get; private set; }

    /// <summary>Subaba "Itens e animais": conferência e o que foi escolhido no jogo.</summary>
    [ObservableProperty]
    public partial string SuppliesMarkdown { get; private set; } = "";

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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnalysis))]
    public partial EmbarkAnalysis? Analysis { get; private set; }

    public bool HasAnalysis => Analysis is not null;

    /// <summary>Plano de pontos de todos os anões (editado na ficha ou colado da IA).</summary>
    public EmbarkPlan Plan { get; } = new();

    /// <summary>Subaba escolhida (0 = Anões, 1 = Itens e animais, 2 = Resumo/IA).</summary>
    [ObservableProperty]
    public partial int SelectedTab { get; set; }

    /// <summary>O que o jogador quer da distribuição (vai no pedido para a IA).</summary>
    [ObservableProperty]
    public partial string AiInstructions { get; set; } = "";

    /// <summary>Copiar o resumo junto com o pedido (para quem não mandou o resumo antes).</summary>
    [ObservableProperty]
    public partial bool IncludeSummaryInRequest { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImportMessage))]
    public partial string ImportMessage { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImportProblems))]
    public partial string ImportProblems { get; private set; } = "";

    [ObservableProperty]
    public partial bool ImportFailed { get; private set; }

    public bool HasImportMessage => ImportMessage.Length > 0;
    public bool HasImportProblems => ImportProblems.Length > 0;

    /// <summary>Resumo do embarque para a IA (só os dados; nulo antes da primeira leitura).</summary>
    public string? SummaryForAi => Analysis is null ? null : EmbarkMarkdown.Build(Analysis, Plan);

    /// <summary>Pedido de distribuição de pontos (regras + formato da resposta), com ou sem o resumo.</summary>
    public string? PointsRequestForAi()
    {
        if (Analysis is null) return null;
        var request = EmbarkAiProtocol.BuildPointsRequest(Analysis, AiInstructions);
        return IncludeSummaryInRequest ? $"{SummaryForAi}\n---\n\n{request}" : request;
    }

    /// <summary>O plano atual no formato do protocolo.</summary>
    public string? PlanForAi() => Analysis is null ? null : EmbarkAiProtocol.Format(Analysis.Snapshot, Plan);

    /// <summary>Lê a resposta da IA (colada) e carrega o plano dos anões que vieram nela.</summary>
    public bool ImportAiResponse(string? text)
    {
        ImportProblems = "";
        if (Analysis is null)
            return ImportResult(false, "Leia o embarque primeiro (Atualizar).");
        if (string.IsNullOrWhiteSpace(text))
            return ImportResult(false, "A área de transferência está vazia. Copie a resposta da IA e tente de novo.");

        var import = EmbarkAiProtocol.Parse(text, Analysis.Snapshot);
        EmbarkAiProtocol.Apply(import, Analysis.Snapshot, Plan);
        ImportProblems = string.Join("\n", import.Errors.Concat(import.Warnings));
        if (import.Levels.Count == 0)
            return ImportResult(false, "Não consegui ler um plano nessa resposta.");

        SelectedTab = 0;
        return ImportResult(true, $"Plano carregado para {import.Levels.Count} anões às {DateTime.Now:HH:mm}. Confira e ajuste na ficha de cada um.");
    }

    private bool ImportResult(bool ok, string message)
    {
        ImportFailed = !ok;
        ImportMessage = message;
        return ok;
    }

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
                SetSupplies("");
                StatusText = $"{prefix}o jogo não está na tela de embarque";
                SetMarkdown(EmbarkMarkdown.NotOnEmbarkScreen(snapshot.Screen));
                return;
            }

            Sheets.Update(snapshot);
            Analysis = EmbarkAnalyzer.Analyze(snapshot);
            LoadSavedPlan(snapshot);
            var captured = snapshot.Dwarves.Count(d => Sheets.For(d.UnitId).Count > 0);
            StatusText = $"{prefix}{snapshot.Dwarves.Count} anões · {captured} com descrição do jogo · atualizado às {DateTime.Now:HH:mm:ss}";

            // O Markdown sem as decisões tem todos os dados do jogo; com a versão das descrições capturadas,
            // diz se as fichas precisam ser refeitas (as decisões ficam no plano e não pedem refazer).
            // Itens e animais ficam fora da assinatura: mudar a compra não refaz as fichas.
            var signature = $"{Sheets.Version}\n{EmbarkMarkdown.Build(Analysis, includeSupplies: false)}";
            SetMarkdown(EmbarkMarkdown.Build(Analysis, Plan));
            SetSupplies(EmbarkSupplies.Build(Analysis, Plan));
            if (signature == _lastSignature && Dwarves.Count == snapshot.Dwarves.Count) return;
            _lastSignature = signature;
            RebuildDwarves(Analysis);
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

    /// <summary>Recria as fichas mantendo o anão selecionado.</summary>
    private void RebuildDwarves(EmbarkAnalysis analysis)
    {
        var selectedIndex = SelectedDwarf?.Dwarf.Index;
        foreach (var old in Dwarves) old.Dispose();
        Dwarves.Clear();
        foreach (var dwarf in analysis.Snapshot.Dwarves)
            Dwarves.Add(new DwarfSheetViewModel(analysis, dwarf, Sheets, Plan, _apply is null ? null : ApplyToGameAsync));
        SelectedDwarf = Dwarves.FirstOrDefault(d => d.Dwarf.Index == selectedIndex) ?? Dwarves.FirstOrDefault();
        OnPropertyChanged(nameof(HasDwarves));
    }

    private void Fail(string message, string? markdown)
    {
        HasError = true;
        StatusText = message;
        if (markdown is not null) SetMarkdown(markdown);
    }

    /// <summary>Só troca o texto se mudou (evita redesenhar e perder a rolagem a cada atualização automática).</summary>
    private void SetMarkdown(string markdown)
    {
        if (markdown != Markdown) Markdown = markdown;
    }

    private void SetSupplies(string markdown)
    {
        if (markdown != SuppliesMarkdown) SuppliesMarkdown = markdown;
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
