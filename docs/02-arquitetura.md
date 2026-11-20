# 02 – Arquitetura

## Projetos

| Projeto | Tipo | Responsabilidade | Depende de |
|---|---|---|---|
| `DwarfVolon.Core` | classlib | Domínio sem UI: world gen, modificadores, Markdown, detecção do jogo, configurações | nada (só a BCL) |
| `DwarfVolon.DFHack` | classlib | Cliente RPC do DFHack (TCP, handshake, chamadas) e scripts Lua embutidos | Core |
| `DwarfVolon` | WinExe (Avalonia 12) | Views e view-models | Core, DFHack, CommunityToolkit.Mvvm |
| `DwarfVolon.Core.Tests` | xUnit | Testes do Core e do cliente DFHack | Core, DFHack |
| `DwarfVolon.DfHackCli` | console | Ferramenta de desenvolvimento para falar com o DFHack | DFHack |
| `DwarfVolon.Tests` | xUnit | Testes dos view-models (sem abrir janela) | DwarfVolon |

Projetos planejados para as próximas fases:

| Projeto | Fase | Responsabilidade |
|---|---|---|
| `DwarfVolon.DFHack.Proto` | quando precisar | Classes C# geradas a partir dos `.proto` do DFHack, para mensagens grandes ([D12](decisoes.md#d12--cliente-rpc-do-dfhack-sem-gerar-código-protobuf)) |
| `DwarfVolon.Agent` | 4 | Integração com LLM (DeepSeek) e definição das ferramentas da IA |

## Camadas

```
┌──────────────────────────────┐
│  Views (Avalonia XAML)       │  só layout e bindings
├──────────────────────────────┤
│  ViewModels (MVVM Toolkit)   │  estado de tela, comandos
├──────────────────────────────┤
│  Serviços do Core            │  ← a IA (Fase 4) usa esta mesma camada
│  WorldGen / DwarfFortress /  │
│  Settings / (DFHack)         │
├──────────────────────────────┤
│  Arquivos do jogo / RPC      │
└──────────────────────────────┘
```

Regra principal: **a UI e a IA nunca falam direto com arquivo ou socket.** As duas chamam os serviços do Core. Assim a mesma ação (ex.: "atribuir mineração ao Urist") existe uma vez só, testada uma vez só.

## Organização do Core

```
DwarfVolon.Core/
  WorldGen/
    WorldGenToken.cs           token [NOME:arg:arg]
    WorldGenParameterSet.cs    um bloco [WORLD_GEN]; edição que preserva o texto
    WorldGenDocument.cs        o arquivo inteiro; parse/escrita sem perdas
    WorldGenTokenCatalog.cs    metadados (nome em pt-BR, descrição, categoria, ordem do jogo)
    WorldGenPreset.cs          patch de operações (set/multiply/add/min/max) + PresetApplier
    WorldGenModifiers.cs       categorias, níveis e perfis; ModifierLibrary; BuiltInTemplates
    WorldGenMarkdown.cs        resumo do mundo e catálogo de modificadores em Markdown
    WorldGenRiskEstimator.cs   estimativa de risco de rejeição (sem gerar o mundo)
    WorldGenRepository.cs      lê/grava prefs/world_gen.txt com backup
    Modifiers/*.json           categorias embutidas e _perfis.json (recurso)
    Resources/*.txt            conjuntos padrão do jogo (recurso)
  Embark/
    EmbarkSnapshot.cs          modelos da tela de embarque + EmbarkJson (parser da saída do script)
    DfNames.cs                 nomes em pt-BR de habilidades, atributos e níveis
    EmbarkRoles.cs             funções da fortaleza (Resources/funcoes.json)
    EmbarkAnalyzer.cs          notas por função, distribuição e níveis sugeridos
    EmbarkPlan.cs              decisões (funções + pontos + origem), regras e protocolo com a IA
    EmbarkPlanStore.cs         decisões salvas por mundo; aplicação no jogo (itens e resultado)
    EmbarkMarkdown.cs          documento do embarque e schema da tabela de anões
    DfRichText.cs              texto com cores das fichas do jogo; EmbarkSheetCache
  Markdown/
    MarkdownTable.cs           tabelas e schemas de colunas (MarkdownTableSchema<T>)
    MarkdownBuilder.cs         montagem de documentos
    MarkdownParser.cs          leitura de volta em blocos/inlines (usado pelo MarkdownView)
  DwarfFortress/
    DwarfFortressInstallation.cs  caminhos (modo portátil, prefs, saves, DFHack)
    DwarfFortressLocator.cs       detecção via bibliotecas da Steam
  Settings/
    AppSettings.cs             settings.json, AppPaths
```

## Organização do app

```
DwarfVolon/
  Views/MainWindow.axaml       menu lateral + página atual (DataTemplates: view-model → view)
  ViewModels/
    MainWindowViewModel.cs     itens do menu; MarkdownPageViewModel
    WorldGenEditorViewModel.cs gerador de mundo (Simples + Avançado + salvar)
    EmbarkViewModel.cs         embarque (lê o jogo pelo DFHack, atualização automática)
    DwarfSheetViewModel.cs     ficha de um anão do embarque (linhas de gráfico prontas)
    PlanEditorViewModel.cs     decisões de um anão (funções, pontos, aplicar no jogo)
    ModifierViewModels.cs      cards de categoria, níveis, perfis
    TokenRowViewModel.cs       linhas do editor avançado
    SettingsViewModel.cs
  Controls/MarkdownView.cs     renderizador de Markdown
  Controls/MeterBar.cs         barra de magnitude ou divergente (gráficos das fichas)
  Controls/DfRichTextView.cs   texto do jogo com as cores do DF
  Services/                    ShellHelper (abrir pastas), ClipboardHelper
```

Para adicionar uma página: criar o view-model, a view, um `DataTemplate` em `MainWindow.axaml` e um `NavItemViewModel` em `MainWindowViewModel`.

## Convenções

- **C# moderno**: `Nullable` ligado, `ImplicitUsings`, namespaces de arquivo.
- **MVVM**: `CommunityToolkit.Mvvm` com propriedades parciais (`[ObservableProperty] public partial ...`) e `[RelayCommand]`.
- **Bindings compilados**: toda view e todo `DataTemplate` declara `x:DataType`.
- **Code-behind** só para coisas que precisam da janela (ex.: seletor de pasta via `StorageProvider`).
- **Textos da UI e docs em pt-BR.** Identificadores no código em inglês.
- **Testes**: todo comportamento do Core que mexe em arquivo do jogo precisa de teste, inclusive o de "ler e salvar sem mudanças gera arquivo idêntico".

## Onde ficam os dados

| O quê | Onde |
|---|---|
| Configurações do app | `%APPDATA%\DwarfVolon\settings.json` |
| Decisões do embarque (por mundo) | `%APPDATA%\DwarfVolon\embarques\<pasta do mundo>.json` |
| Modificadores do usuário | `%APPDATA%\DwarfVolon\modificadores\*.json` |
| world_gen.txt do jogo | `<dados do usuário DF>\prefs\world_gen.txt` (ver [world gen](04-world-gen.md#onde-fica-o-arquivo)) |
| Backups do world_gen | `<dados do usuário DF>\prefs\dwarfvolon-backups\` |
