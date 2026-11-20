# 08 – Embarque

A página **Embarque** lê os anões da tela **Preparar com cuidado** (*Prepare carefully*) do jogo aberto, mostra uma ficha de cada um e guarda as **decisões** do jogador: as **funções** de cada anão e o **plano de pontos** de habilidade. O plano pode ser aplicado no jogo (com confirmação) e as decisões ficam salvas por mundo, para depois do embarque.

Nada é decidido automaticamente: funções e pontos vêm do jogador, de uma IA (resposta colada) ou do botão **Sugerir**.

## Como usar

1. Abra o Dwarf Fortress com o DFHack.
2. Escolha o local e clique em **Preparar com cuidado**.
3. No DwarfVolon, abra **Embarque** (a página lê o jogo ao ser aberta) ou clique em **Atualizar**.
4. Com **Atualizar automaticamente** ligado, a página relê o jogo a cada 3 segundos e acompanha as mudanças feitas lá (e captura o texto das fichas abertas no jogo).
5. Na subaba **Anões**, decida funções e pontos na ficha de cada anão, ou converse com uma IA pela subaba **Resumo (IA)** e cole a resposta.
6. **Aplicar no jogo** (na ficha) ou **Aplicar todos no jogo** (no topo) escreve os pontos na tela do jogo, depois de mostrar o que vai mudar.

## Subabas

| Subaba | Para quê |
|---|---|
| **Anões** | Lista à esquerda (nome, funções, alertas) e a ficha do anão à direita. No topo: **Sugerir funções para todos**, **Aplicar todos no jogo** e onde as decisões estão salvas |
| **Resumo (IA)** | Os passos para conversar com uma IA e o resumo em Markdown (o que é copiado) |

### Ficha do anão

- **Cabeçalho**: nome, nome traduzido, sexo, idade e alertas de personalidade.
- **Decisões** (cartão no topo):
  - **Origem**: "Veio da resposta da IA colada às 15:02" (em âmbar), "Funções sugeridas pelo app" ou "Montado à mão", com "com ajustes seus" quando alguém mexeu depois.
  - **Funções**: selos com ✕, lista para adicionar e o botão **Sugerir** (análise de atributos). Depois do embarque, só quem tem a função faz aquele trabalho.
  - **Plano de pontos**: habilidades com − e +, ✕ para tirar e uma lista para adicionar (as 123 habilidades da tela de embarque). Mostra o saldo ("7 de 10 pontos", vermelho se passar) e a comparação com o jogo ("Igual ao jogo ✓" ou "2 mudanças ainda não aplicadas no jogo"). **Limpar** tira os pontos (as funções ficam).
  - **Aplicar no jogo**: mostra "Vai mudar no jogo: Mineração 0 → 1, ..." e só escreve depois de **Confirmar**; em seguida relê o jogo.
- **Atributos físicos e mentais**: barras divergentes a partir da mediana da raça (traço): azul acima, vermelho abaixo. Passe o mouse para ver o valor do jogo e a mediana.
- **Aptidão por função**: barras ordenadas (traço = mediano), funções do anão em destaque e a posição dele entre todos (ex.: "2º").
- **Personalidade**: só as facetas fora do neutro (40–60), da mais forte para a mais fraca.
- **Crenças e sonho**: crenças em que o anão difere da cultura (-50 a +50) e o sonho de vida.
- **Gostos**: por grupo (materiais, comida, itens...), com os nomes do jogo em inglês; ódios por último.
- **Habilidades**: as escolhidas no embarque e as que o anão já traz (música, poesia...), que não gastam pontos.
- **Descrição do jogo**: o texto original, com as cores do jogo (verde = qualidade, vermelho = defeito).

Os gráficos usam um controle próprio (`Controls/MeterBar.cs`): barra fina, ponta arredondada, trilho cinza neutro e o par azul/vermelho da paleta de referência (com tons para tema escuro). Barras e não radar: 19 eixos num radar ficam ilegíveis.

### Descrição do jogo (texto original)

O DF só gera o texto da ficha quando ela está aberta no jogo. Ele fica em `game.main_interface.view_sheets`: `open`, `active_id` (id da unidade), `active_sub_tab` (10 = Personalidade no DF 53.17), `personality_active_tab` e `personality_raw_str` (blocos de texto). Com a aba de personalidade aberta vêm 7 blocos: atributos mentais ("She has an amazing spatial sense..."), traços ("She can be very single-minded..."), hábitos e necessidades ("She needs alcohol...").

- Marcações: `[C:cor:fundo:brilho]` muda a cor (2 = verde, 4 = vermelho, 7 = normal) e `[P]`/`[B]`/`[R]` quebram parágrafo. A leitura fica em `Core/Embark/DfRichText.cs`.
- O `EmbarkSheetCache` guarda o texto por anão e por aba de personalidade durante a sessão do app. Com *Atualizar automaticamente* ligado, basta abrir a ficha de cada anão no jogo uma vez. O status mostra quantos anões já têm descrição.
- Só a aba Personalidade é capturada (pelo `active_sub_tab`), para não guardar texto velho de outra aba.
- O texto **não** vai para o resumo da IA (gastaria contexto e repete o que os números dizem).

## Decisões salvas por mundo

As decisões são salvas a cada mudança em `%APPDATA%\DwarfVolon\embarques\<pasta do mundo>.json` (ex.: `region2.json`; a pasta vem de `world.cur_savegame.save_dir`). Ao ler a tela de embarque de um mundo pela primeira vez, o app restaura o que estava salvo.

Cada anão é guardado com a **figura histórica** (`hist_figure_id`, que continua a mesma depois do embarque), o id da unidade e o nome, mais as funções (ids de `funcoes.json`), o plano de pontos e a origem. Ao restaurar, o anão é reconhecido nessa ordem: figura histórica, unidade, nome.

Uso planejado depois do embarque (Fase 3): ler este arquivo na fortaleza e designar os trabalhos pelas funções (ex.: só os anões com **Minerador** no *work detail* de mineração).

## Aplicar no jogo

`EmbarkWriter` manda o plano em JSON junto com o script [`embark_apply.lua`](../src/DwarfVolon.DFHack/Scripts/embark_apply.lua):

- Confere que o jogo está na tela de embarque e que cada anão ainda é o mesmo (`unit_id`).
- Valida tudo antes de mudar qualquer coisa (ou aplica todos, ou nenhum): habilidade existe, nível 1–5, custo dentro do limite.
- Para cada anão, as habilidades escolhidas passam a ser exatamente as do plano (as que ele já traz não mudam) e `skill_picks_left` é recalculado.

> **Regras ainda supostas** (`EmbarkPlanRules`): 1 ponto por nível, nível 1 a 5, limite = pontos livres + níveis já escolhidos, e comprar níveis não mexe nos pontos de embarque gerais. Precisam ser conferidas na tela do jogo depois da primeira aplicação.

## Conversa com a IA (protocolo)

Na subaba **Resumo (IA)**, três passos separados, para poder conversar com a IA entre eles:

1. **Copiar resumo**: os dados dos anões e as decisões já tomadas. Mande primeiro e converse à vontade (análise, recomendações, planos para a fortaleza).
2. **Copiar pedido de pontos**: só o pedido, sem o resumo (opção para incluir). Leva as instruções do jogador (caixa de texto), as regras, o limite de cada anão pelo nome, as funções e as habilidades disponíveis e o formato da resposta.
3. **Colar resposta da IA**: lê a área de transferência, acha o bloco, carrega funções e pontos nas fichas e volta para a subaba Anões. Erros e avisos aparecem ao lado.

**Copiar plano atual** gera o mesmo formato a partir das fichas (para mostrar à IA ou guardar).

Formato da resposta: um bloco de código com a linguagem `dwarfvolon-pontos`, uma linha por anão, com as funções opcionais depois de `| funções:`:

````
```dwarfvolon-pontos
Besmar Ustuthkifed: SET_BONE 5, SURGERY 4, CARPENTRY 1 | funções: Médico, Carpinteiro
èrith Eribzuden: DIAGNOSE 5, SUTURE 2, DRESS_WOUNDS 2, MINING 1 | funções: Médico, Minerador
```
````

Leitura tolerante (`EmbarkAiProtocol.Parse`):

- Nome sem acento, em qualquer caixa, ou só o primeiro nome (se não for ambíguo); marcadores de lista e negrito são ignorados.
- Habilidade pelo ID do jogo, pelo nome em pt-BR ("Diagnóstico") ou pelo nome do jogo ("Bone Setting"). Função pelo nome ou id ("Médico", "medico").
- Sem o bloco (a IA esqueceu as crases), tenta as linhas `nome: habilidades` do texto todo.
- Anão que aparece tem os pontos trocados pela lista (e as funções, se a linha trouxer `| funções:`); anão que não aparece fica como está.
- Nível acima de 5 vira 5 (aviso); passar do limite de pontos carrega, mas avisa; anão, habilidade ou função desconhecida é listada como erro.

O mesmo protocolo vai servir para o chat integrado (Fase 4): a IA devolve o bloco e o app carrega nas fichas, sempre com revisão antes de aplicar.

## Botão Sugerir (análise de atributos)

O app só sugere quando o jogador pede (**Sugerir** na ficha ou **Sugerir funções para todos**). A sugestão preenche apenas as funções; os pontos ficam com o jogador ou a IA.

1. **Nota de atributo (0–100).** O jogo informa a faixa de cada atributo para a casta (7 pontos: mínimo … mediana … máximo). A nota interpola nessa faixa: mínimo = 0, mediana = 50, máximo = 100. Ex.: Força 1250 num anão = 50.
2. **Nota por função.** Média das notas dos atributos que ajudam na habilidade da função. As funções, os atributos, a prioridade e a quantidade (ex.: 2 mineradores) estão em [`Embark/Resources/funcoes.json`](../src/DwarfVolon.Core/Embark/Resources/funcoes.json). O mapeamento habilidade → atributos é **aproximado** (wiki do DF).
3. **Quem faz o quê.** As vagas (funções × quantidade, por prioridade) são distribuídas em rodadas: na 1ª, cada anão recebe uma função **principal**; na 2ª, uma **secundária**. Em cada rodada é escolhida a melhor combinação possível (algoritmo húngaro), e não "o melhor para a primeira vaga". Funções mais prioritárias pesam um pouco mais, e quem já tem a habilidade ganha preferência.

As notas por função também aparecem no resumo (tabela "Nota por função") como dado para a IA.

## Por dentro

```
DwarfVolon.DFHack/
  Scripts/embark.lua         lê viewscreen_setupdwarfgamest → JSON
  Scripts/embark_apply.lua   escreve o plano de pontos (valida antes de mudar)
  EmbarkReader.cs            roda embark.lua pelo comando lua (D13) e converte
  EmbarkWriter.cs            manda o plano + embark_apply.lua
DwarfVolon.Core/Embark/
  EmbarkSnapshot.cs   modelos + EmbarkJson (parser tolerante: [] vazio do Lua vira dicionário)
  DfNames.cs          nomes em pt-BR (habilidades, atributos, facetas, crenças, sonhos)
  EmbarkRoles.cs      catálogo de funções (funcoes.json)
  EmbarkAnalyzer.cs   notas e o botão Sugerir (húngaro)
  EmbarkPlan.cs       decisões (funções + pontos + origem), regras e o protocolo com a IA
  EmbarkPlanStore.cs  arquivo por mundo; itens/resultado da aplicação no jogo
  EmbarkMarkdown.cs   resumo para a IA e schema da tabela de anões
  DfRichText.cs       texto com cores das fichas do jogo + EmbarkSheetCache
DwarfVolon/
  ViewModels/EmbarkViewModel.cs       página (ler, salvar, sugerir, aplicar, IA)
  ViewModels/DwarfSheetViewModel.cs   ficha de um anão (linhas de gráfico prontas)
  ViewModels/PlanEditorViewModel.cs   decisões de um anão (funções, pontos, aplicar)
  Views/DwarfSheetView.axaml          ficha
  Controls/MeterBar.cs                barra de magnitude ou divergente
  Controls/DfRichTextView.cs          texto do jogo com cores
```

Campos usados da tela do jogo:

- `dwarf_info[i]` (`setup_character_info`): só o que se escolhe na tela, `skilllevel[]` e `skill_picks_left`. Nome e idade daqui vêm vazios no embarque de fortaleza.
- `s_unit[i]` (a unidade): nome, idade (`dfhack.units.getAge`), sexo, figura histórica, atributos (`dfhack.units.get*AttrValue`) e a alma (`status.current_soul`): habilidades que já traz, facetas, gostos (`preferences`), crenças (`personality.values`) e sonhos (`personality.dreams`).
- `points_remaining`, `embark_skills[aba]`, as faixas da casta (`creature_raw.caste[].attributes`), a ficha aberta (`view_sheets`), o nome do mundo e a pasta do save.

Linha de comando: `dotnet run --project tools/DwarfVolon.DfHackCli -- embark` (resumo) e `-- embark-pedido "instruções"` (pedido para a IA).

## Próximos passos

- Conferir na tela do jogo as regras de custo depois da primeira aplicação (pontos do anão × pontos de embarque).
- Fase 3: na fortaleza, designar os trabalhos pelas funções salvas (ex.: só os mineradores mineram).
- Conferir o que as outras abas de personalidade da ficha do jogo geram (gostos, crenças) e se a captura pega.
- Itens e animais do embarque.
- Permitir funções do usuário em `%APPDATA%\DwarfVolon\` (como os modificadores do world gen).
