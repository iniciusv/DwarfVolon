# 07 – Módulo Markdown

## Por quê

Markdown é o formato que modelos de linguagem leem com mais precisão, e também é fácil de ler por pessoas. A ideia é que ele seja a **língua comum** do DwarfVolon:

- as telas mostram dados como documentos e **tabelas Markdown** renderizadas;
- o mesmo texto pode ser **copiado** ou **enviado para a IA** como contexto, sem conversão;
- a IA recebe junto um "catálogo" do que pode pedir ou fazer (ex.: as categorias de modificadores e seus `id`s).

Exemplo do objetivo: uma tabela compacta com os anões disponíveis no embarque (habilidades, atributos, preferências) e os pontos de embarque restantes. Com ela, a IA sugere a distribuição de pontos.

## O que existe

Tudo em [`DwarfVolon.Core/Markdown`](../src/DwarfVolon.Core/Markdown/) (sem dependência de UI) e um controle na interface.

| Peça | Para quê |
|---|---|
| `MarkdownTableSchema<T>` | Catálogo de colunas possíveis para um tipo de linha (ex.: anão). Cada coluna tem `key`, cabeçalho, função de valor, alinhamento e descrição. `Render(rows, ["nome", "mineracao"])` gera a tabela só com as colunas pedidas, na ordem pedida. `DescribeColumns()` lista as colunas disponíveis (útil para a IA saber o que pode pedir). |
| `MarkdownTable` | Tabela a partir de cabeçalhos e linhas soltas; escapa `\|` e quebras de linha; formata `bool` como sim/não e números em cultura invariante. |
| `MarkdownTableOptions` | `Compact` (padrão: sem espaços extras, economiza contexto da IA) ou `Readable` (colunas alinhadas, melhor como texto puro). |
| `MarkdownBuilder` | Monta documentos: títulos, parágrafos, listas, citações, código, linhas, tabelas e tabelas campo/valor. |
| `MarkdownParser` | Lê o subconjunto acima de volta em blocos (`MdHeading`, `MdTable`...) e inlines (`**negrito**`, `*itálico*`, `` `código` ``). |
| `MarkdownView` (app) | Controle Avalonia que renderiza Markdown: títulos, listas, citações, código e tabelas com cabeçalho e linhas alternadas. Texto selecionável. |
| `MarkdownPageViewModel` (app) | Página inteira feita de um documento Markdown, com botão **Copiar Markdown**. Usado hoje nas páginas "em breve". |

### Exemplo

```csharp
var schema = new MarkdownTableSchema<Dwarf>()
    .Add("nome", "Nome", d => d.Name)
    .Add("prof", "Profissão", d => d.Profession)
    .Add("mineracao", "Mineração", d => d.Skills.Mining, MdAlign.Right, "Nível de habilidade (0–20)")
    .Add("ocioso", "Ocioso", d => d.IsIdle);

var md = new MarkdownBuilder()
    .Heading(1, "Anões ociosos")
    .Table(schema, dwarves.Where(d => d.IsIdle), ["nome", "prof", "mineracao"])
    .ToString();
```

Saída compacta:

```
# Anões ociosos

|Nome|Profissão|Mineração|
|---|---|--:|
|Bomrek|Pedreiro|3|
```

## Onde já é usado

- **Gerador de mundo → Avançado → Markdown**: resumo do mundo (base, nível de cada categoria, parâmetros alterados com antes/depois). Botões **Copiar resumo** e **Copiar catálogo de modificadores**. O painel de resumo também tem **Copiar Markdown**.
- **Páginas Anões e Assistente**: o conteúdo "em breve" é um documento Markdown, incluindo uma tabela de exemplo.

## Próximos passos

- **Seletor de colunas na UI**: a partir de um `MarkdownTableSchema<T>`, mostrar as colunas como caixas de marcar, renderizar a tabela e permitir copiar. Vai ser o componente da tela de Anões.
- **Schemas da fortaleza** (Fase 2, via DFHack): anões, habilidades, humor, ferimentos, estoques.
- **Embarque**: tabela dos anões disponíveis e da distribuição de pontos, mais um bloco "o que você pode fazer" para a IA.
- **Contexto da IA** (Fase 4): as ferramentas de leitura devolvem Markdown gerado por estes mesmos schemas, e o catálogo de ações vai junto.
- **Limite de tamanho**: cortar tabelas grandes (top N + "e mais X linhas") para não estourar o contexto.
