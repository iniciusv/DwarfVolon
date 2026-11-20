# 08 – Embarque

A página **Embarque** lê os anões da tela **Preparar com cuidado** (*Prepare carefully*) do jogo aberto e sugere quem faz o quê e onde gastar os pontos de habilidade. Ela só lê: nada é alterado no jogo.

## Como usar

1. Abra o Dwarf Fortress com o DFHack.
2. Escolha o local e clique em **Preparar com cuidado**.
3. No DwarfVolon, abra **Embarque** (a página lê o jogo ao ser aberta) ou clique em **Atualizar**.
4. Com **Atualizar automaticamente** ligado, a página relê o jogo a cada 3 segundos e acompanha as mudanças que você fizer lá.
5. **Copiar Markdown** copia tudo para colar numa IA.

## O que aparece

| Seção | Conteúdo |
|---|---|
| Sugestão de funções | Função, anão, nota, nível atual, nível sugerido e o motivo (atributos fortes e fracos) |
| Anões | Nome, sexo, idade, habilidades atuais, pontos livres, plano sugerido e traços de personalidade que pesam no humor |
| Nota por função | Matriz anão × função (0–100), com o melhor de cada função em negrito |
| Atributos | Nota de cada atributo físico e mental (0–100) |
| Funções consideradas | Habilidade e atributos usados em cada função |

## Como a sugestão é calculada

1. **Nota de atributo (0–100).** O jogo informa a faixa de cada atributo para a casta (7 pontos: mínimo … mediana … máximo). A nota interpola nessa faixa: mínimo = 0, mediana = 50, máximo = 100. Ex.: Força 1250 num anão = 50.
2. **Nota por função.** Média das notas dos atributos que ajudam na habilidade da função. As funções, os atributos, a prioridade e a quantidade (ex.: 2 mineradores) estão em [`Embark/Resources/funcoes.json`](../src/DwarfVolon.Core/Embark/Resources/funcoes.json). O mapeamento habilidade → atributos é **aproximado** (wiki do DF).
3. **Quem faz o quê.** As vagas (funções × quantidade, por prioridade) são distribuídas em rodadas: na 1ª, cada anão recebe uma função **principal**; na 2ª, uma **secundária**. Em cada rodada é escolhida a melhor combinação possível (algoritmo húngaro), e não "o melhor para a primeira vaga". Funções mais prioritárias pesam um pouco mais, e quem já tem a habilidade ganha preferência.
4. **Níveis.** Os pontos de habilidade do anão (livres + já gastos) são divididos assim: 1 nível em cada função; o resto enche a principal até 5 (Proficiente) e depois a secundária.

> A ser conferido na tela real: a relação entre os pontos de habilidade de cada anão (`skill_picks_left`) e os pontos de embarque gerais (`points_remaining`). Se comprar níveis também gastar pontos de embarque, a regra dos níveis precisa considerar isso.

## Por dentro

```
DwarfVolon.DFHack/Scripts/embark.lua   lê viewscreen_setupdwarfgamest → JSON
DwarfVolon.DFHack/EmbarkReader.cs      roda o script pelo comando lua (D13) e converte
DwarfVolon.Core/Embark/
  EmbarkSnapshot.cs   modelos + EmbarkJson (parser tolerante: [] vazio do Lua vira dicionário)
  DfNames.cs          nomes em pt-BR (habilidades, atributos, níveis)
  EmbarkRoles.cs      catálogo de funções (funcoes.json)
  EmbarkAnalyzer.cs   notas, distribuição (húngaro) e níveis
  EmbarkMarkdown.cs   documento e schema da tabela de anões
DwarfVolon/ViewModels/EmbarkViewModel.cs   página (atualizar, automático, copiar)
```

Campos usados da tela do jogo: `dwarf_info[i]` (`setup_character_info`: `name`, `skilllevel[]`, `skill_picks_left`, `birth_year`, `pers`), `s_unit[i]` (atributos via `dfhack.units.get*AttrValue`), `points_remaining`, `embark_skills[aba]` e as faixas da casta (`creature_raw.caste[].attributes`).

Para testar sem a interface: `dotnet run --project tools/DwarfVolon.DfHackCli -- embark`.

## Próximos passos

- Validar na tela real (custo dos níveis, nomes, idades).
- Preferências (gostos) dos anões e itens/animais do embarque na tabela.
- Permitir funções do usuário em `%APPDATA%\DwarfVolon\` (como os modificadores do world gen).
- Aplicar a sugestão no jogo (escrita, com confirmação; Fase 3).
- Mandar as tabelas para a IA refinar a sugestão (Fase 4).
