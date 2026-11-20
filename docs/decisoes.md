# Registro de decisões

Formato curto: contexto → decisão → consequências. Decisões novas vão no fim, com o próximo número.

## D1 – C# em vez de F#

**Contexto.** O projeto começou de um template Avalonia em F#. O plano inclui MVVM, gerar classes de protobuf (DFHack) e integrar SDKs de IA.

**Decisão.** Converter para C# (.NET 10, Avalonia 12).

**Consequências.**
- Dá para usar `CommunityToolkit.Mvvm`, cujos source generators não funcionam em F#.
- O `Grpc.Tools` gera C# direto, sem precisar de um projeto C# separado só para isso.
- Exemplos, templates e documentação da Avalonia e dos SDKs de IA são quase todos em C#.

## D2 – Começar pelo editor de world gen

**Contexto.** As três frentes (world gen, anões, IA) têm valor, mas as duas últimas dependem do DFHack e do jogo aberto.

**Decisão.** A Fase 1 é o editor de world gen, que só mexe em arquivo.

**Consequências.** Entrega valor logo, sem risco para saves, e firma a arquitetura (Core sem UI, testes, presets) antes da parte de rede.

## D3 – Parser que preserva o arquivo

**Contexto.** O `world_gen.txt` é do jogador e pode ter conjuntos feitos à mão, comentários e dados do World Painter.

**Decisão.** Parser orientado a linhas: cada linha guarda o texto original e só linhas alteradas são reescritas. Leitura e escrita em Latin-1.

**Consequências.** Ler e salvar sem mudanças gera arquivo idêntico (há teste para isso). Comentários na mesma linha de um token alterado se perdem, o que é aceitável.

## D4 – Patches multiplicativos

**Contexto.** Os mínimos do world gen (rios, vulcões, regiões) escalam com o tamanho do mundo. Um preset com números absolutos quebraria mundos pequenos ou não faria diferença nos grandes.

**Decisão.** Presets são listas de operações (`set`, `multiply`, `add`, `min`, `max`) aplicadas sobre uma base, de preferência multiplicando.

**Consequências.** O mesmo preset funciona em qualquer tamanho e presets podem ser combinados. Em troca, o resultado depende da base, por isso a UI sempre mostra o antes → depois.

## D5 – Padrões do DF 0.47 como base embutida

**Contexto.** O DF 53 não traz mais `data/init/world_gen.txt`; os padrões ficam dentro do executável.

**Decisão.** Embutir os 12 conjuntos padrão da 0.47 como base, e documentar o `export-worldgen` do DFHack para quem quiser uma base 100% atual.

**Consequências.** Funciona sem o jogo aberto. Tokens novos (ex.: `NIGHTMARE_NUMBER`) ficam ausentes e o jogo usa os padrões dele. Os modificadores embutidos só usam tokens que existem nessa base (há teste para isso).

## D6 – Detectar o jogo pela Steam e respeitar o modo portátil

**Decisão.** Procurar `steamapps/common/Dwarf Fortress` em todas as bibliotecas do `libraryfolders.vdf`. Usar `<instalação>/prefs` se existir `prefs/portable.txt`; senão `%APPDATA%/Bay 12 Games/Dwarf Fortress/prefs`.

**Consequências.** Configuração zero na maioria dos casos; para outras instalações há o campo manual.

## D7 – MCP existente só como referência

**Decisão.** O `dfhack-mcp` (Node) não entra como dependência. Ele serve de referência para o desenho das ferramentas da IA; a comunicação com o DFHack será feita em C#.

**Consequências.** Um único runtime (.NET). Fica aberta a opção de o DwarfVolon expor o próprio servidor MCP.

## D8 – Categorias com níveis em vez de presets soltos

**Contexto.** A primeira versão tinha presets marcáveis que se acumulavam ("Muitos rios" + "Rios e vulcões" multiplicava duas vezes) e uma tela centrada em parâmetros. O objetivo é não precisar mexer em parâmetro nenhum.

**Decisão.** Organizar em **categorias** (Rios, Vulcanismo, Cavernas...) com **níveis mutuamente exclusivos** (Poucos / Padrão / Mais...). Combinações prontas viram **perfis**. A edição por parâmetro vai para a aba **Avançado**. Calibração inicial: região média (129×129).

**Consequências.** Escolhas previsíveis (sem multiplicação acumulada dentro de uma categoria) e uma tela simples de entender. O motor (patches) continua o mesmo, e categorias novas são só JSON.

## D9 – Markdown como formato comum entre telas e IA

**Contexto.** A IA vai precisar de contexto sobre a fortaleza (anões, embarque, opções). Modelos leem Markdown, principalmente tabelas, com boa precisão.

**Decisão.** Módulo Markdown genérico no Core (`MarkdownTableSchema<T>`, `MarkdownBuilder`, `MarkdownParser`) e um `MarkdownView` na UI. Dados tabulares que fizerem sentido para a IA são gerados como Markdown e **mostrados na tela a partir desse mesmo texto**.

**Consequências.** O que o usuário vê é exatamente o que a IA recebe. As tabelas compactas economizam contexto. O renderizador é próprio (subconjunto simples), para não depender de bibliotecas que podem não acompanhar o Avalonia 12.

## D10 – Janela com menu lateral

**Decisão.** A janela é um shell com menu lateral e páginas (view-models + DataTemplates); o gerador de mundo é só uma das páginas.

**Consequências.** Novas ferramentas (Anões, Embarque, Assistente) entram sem mexer nas existentes.

## D11 – Mudar a distribuição, não só os mínimos

**Contexto.** A primeira versão dos níveis subia mínimos (`*_RANGES`, `REGION_COUNTS`) e mexia em parâmetros vizinhos (chuva e erosão em "Mais rios", rocha acima da primeira caverna em "Magma perto da superfície"). Isso aumentava a chance de rejeição sem garantir o efeito.

**Decisão.** Cada nível mexe no mínimo de tokens possível. Mais de algo vem de mudar a **distribuição** (`*_FREQUENCY`); quando a distribuição muda, os mínimos das faixas que encolhem são reduzidos. Cada token pertence a uma categoria só. As regras se apoiam nas mensagens de rejeição do executável do DF 53 (por exemplo: não existe rejeição por falta de rios).

**Consequências.** Combinações previsíveis e risco estimado baixo na maioria dos casos. Um estimador (`WorldGenRiskEstimator`) checa isso nos testes e na UI, e o laboratório valida no jogo.


## D12 – Cliente RPC do DFHack sem gerar código protobuf

**Contexto.** O plano era gerar classes C# dos `.proto` do DFHack (`Grpc.Tools`). Para a Fase 2 só precisamos de três chamadas: `BindMethod`, `RunCommand` e as de versão (`GetVersion`, `GetDFVersion`). As mensagens delas só têm strings e inteiros.

**Decisão.** `DwarfVolon.DFHack` tem um codificador protobuf mínimo escrito à mão (`Protocol/Proto.cs`: varint, string e mensagem aninhada) e não depende de nenhum pacote. O projeto `DwarfVolon.DFHack.Proto` fica para quando precisarmos de mensagens grandes (ex.: mapa via RemoteFortressReader).

**Consequências.** Sem dependências nem etapa de geração. O protocolo é testado contra um servidor DFHack falso (`DfHackClientTests`).

## D13 – Scripts Lua enviados pelo comando `lua`, sem instalar nada no jogo

**Contexto.** O padrão "Lua → JSON" (ver [05](05-integracao-dfhack.md)) previa copiar os scripts para `dfhack-config/scripts/dwarfvolon/`.

**Decisão.** Os scripts ficam embutidos no assembly (`DwarfVolon.DFHack/Scripts/*.lua`) e vão inteiros como argumento do comando `lua` do DFHack (`RunCommand("lua", [código])`). O script imprime uma linha de JSON e trata os próprios erros (`{"ok":false,"error":...}`), porque o comando `lua` devolve sucesso mesmo quando o código falha.

**Consequências.** O app não escreve na pasta do jogo, e o script sempre corresponde à versão do app. Os mesmos arquivos ainda podem ser testados no console com `lua -f`. Cuidados: o ambiente do comando `lua` persiste entre chamadas (variáveis globais sobrevivem), e números com decimais saem com vírgula no locale do jogo, então os scripts só devolvem inteiros.

## D14 – Sugestão de embarque por heurística explicável

**Contexto.** A ajuda na distribuição de pontos do embarque precisava funcionar antes do assistente de IA (Fase 4).

**Decisão.** Uma heurística determinística no Core (`EmbarkAnalyzer`): nota de cada atributo comparada com a faixa da casta lida do jogo (mediana = 50), nota por função = média dos atributos da função (`funcoes.json`, editável) e distribuição das funções com o algoritmo húngaro por rodada (principal e depois secundária). O resultado é um documento Markdown com o motivo de cada escolha.

**Consequências.** Funciona sem IA e é testável. O mapeamento de habilidade para atributos é aproximado (wiki do DF). As mesmas tabelas servem de contexto para a IA refinar a sugestão depois.

## D15 – Descrições dos anões: texto original do jogo, capturado da ficha aberta

**Contexto.** A ficha do anão precisava das descrições do jogo ("She is quick to anger..."). O DFHack não expõe o gerador desses textos; o DF só os monta quando a ficha da unidade está aberta (`view_sheets.personality_raw_str`). As alternativas eram escrever textos próprios em pt-BR (cerca de 300 frases) ou fazer o app abrir a ficha no jogo (mexer na interface do jogo).

**Decisão.** Usar o texto original, em inglês, capturado na leitura normal do embarque quando a ficha de um dos anões está aberta na aba Personalidade. O app guarda por anão e aba (`EmbarkSheetCache`) e mostra com as cores do jogo. Os nomes do jogo (gostos, materiais, criaturas) também ficam em inglês.

**Consequências.** Texto exato e sem traduções para manter, mas é preciso abrir a ficha de cada anão uma vez (com a atualização automática ligada). Os dados numéricos (barras) não dependem disso. O texto guardado dura só a sessão do app.

## D16 – Protocolo de texto com a IA: pedido separado do contexto e resposta em bloco marcado

**Contexto.** A IA deve poder sugerir a distribuição de pontos, e o jogador quer conversar antes (análise, objetivos da fortaleza) e só depois pedir a distribuição. O chat integrado (Fase 4) ainda não existe.

**Decisão.** Três peças copiáveis em separado: o **resumo** (contexto), o **pedido** (instruções do jogador, regras, limites por anão, habilidades disponíveis e formato da resposta) e a **resposta**, que a IA devolve num bloco de código `dwarfvolon-pontos` (uma linha `Nome: ID nível, ...` por anão). O app lê o bloco com tolerância (acentos, nomes em pt-BR, sem crases) e só carrega no plano da ficha; nada vai para o jogo sem revisão. O resumo não leva as descrições capturadas das fichas, para não gastar contexto.

**Consequências.** Funciona hoje com qualquer IA de chat, sem integração, e o jogador decide quando mandar cada parte. O mesmo formato serve para o chat integrado e para guardar planos. Pedidos para outras tarefas seguem o mesmo padrão: um bloco com linguagem própria `dwarfvolon-<tarefa>`.

## D17 – Decisões do embarque: nada automático, salvas por mundo, aplicadas com confirmação

**Contexto.** A primeira versão mostrava funções e níveis sugeridos automaticamente, e o jogador não sabia o que tinha vindo do app, da IA ou dele mesmo. Ele também quer que as escolhas do embarque (ex.: quem são os mineradores) sirvam depois, na fortaleza.

**Decisão.** Funções e pontos são **decisões** (`EmbarkPlan`) que só mudam pelo jogador (editor da ficha), pela IA (resposta colada) ou pelo botão **Sugerir** (só funções). Cada anão guarda a origem. As decisões são salvas a cada mudança em `%APPDATA%\DwarfVolon\embarques\<pasta do mundo>.json`, com a figura histórica de cada anão (que continua a mesma depois do embarque). O plano de pontos pode ser escrito no jogo (`embark_apply.lua`): mostra o que vai mudar, pede confirmação, valida tudo antes de mudar e relê a tela depois.

**Consequências.** O resumo para a IA leva as decisões já tomadas, não sugestões. A Fase 3 pode usar o arquivo salvo para designar os trabalhos. As regras de custo dos níveis ainda são supostas (1 ponto por nível, até 5, sem gastar pontos de embarque) e precisam ser conferidas na tela do jogo.
