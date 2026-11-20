# 04 – Gerador de mundo

## Como usar

A tela tem duas abas e um painel fixo à direita.

**Simples** (o caminho normal, sem mexer em parâmetro nenhum):

1. Opcional: clique num **perfil rápido** ("Rios e vulcões", "Fortaleza segura"...) para preencher várias categorias de uma vez.
2. Em cada **categoria** (Rios, Vulcanismo, Relevo, Minérios, Perigo, Cavernas, Camadas de caverna, História, Embarque), escolha um nível. Cards alterados ficam com borda âmbar.
3. Confira o **Resumo** à direita, ajuste o título se quiser e clique em **Salvar no jogo**.
4. No jogo: **Create New World → Design New World with Advanced Parameters**, e escolha o conjunto pelo título.

**Avançado** (para ver ou ajustar tudo):

- **Base**: o conjunto de partida. O padrão é `MEDIUM REGION` (129×129); também dá para usar outro padrão do jogo ou um conjunto do seu `world_gen.txt`. O tamanho do mundo vem da base.
- **Parâmetros**: todos os tokens, agrupados e explicados, já com os modificadores aplicados. Linhas com borda âmbar diferem da base. Ajustes feitos aqui ficam guardados e são aplicados **por cima** dos modificadores, mesmo que você troque de nível depois. **Desfazer ajustes manuais** limpa tudo.
- **O que mudou**: antes → depois de cada token, por modificador.
- **Markdown**: resumo do mundo em tabelas Markdown, com botões para copiar o resumo e o catálogo de modificadores (ver [07 – Markdown](07-markdown.md)).
- **Texto gerado**: o bloco exato que vai para o arquivo.

> Os modificadores foram calibrados para **região média (129×129)**. Com outras bases eles continuam funcionando (a maioria multiplica valores), mas o resumo mostra um aviso.

## Onde fica o arquivo

O jogo (v50+) lê os conjuntos de `prefs/world_gen.txt` dentro da **pasta de dados do usuário**:

| Situação | Pasta de dados do usuário |
|---|---|
| Existe `<instalação>\prefs\portable.txt` (modo portátil) | a própria pasta de instalação |
| Caso contrário (padrão da Steam) | `%APPDATA%\Bay 12 Games\Dwarf Fortress` |

Se o arquivo não existe, o app cria. Antes de alterar um arquivo existente, o app copia ele para `prefs\dwarfvolon-backups\world_gen.<data-hora>.txt`. Salvar com um título que já existe substitui aquele conjunto (o resumo avisa antes).

> Essa regra foi confirmada nas strings do executável do DF 53.17 (`prefs/portable.txt`, `prefs/world_gen.txt`) e é a mesma usada pelo script `export-worldgen` do DFHack.

## Modificadores

Cada **categoria** tem níveis mutuamente exclusivos; um deles é o **padrão** (não muda nada). Por baixo, cada nível é um *patch* de operações sobre os tokens (ver [formato](#formato-de-uma-categoria)).

| Categoria | Níveis | O que mexe |
|---|---|---|
| 🌊 Rios | Poucos · **Padrão** · Mais rios · Muitos rios | `RIVER_MINS` (+ `EROSION_CYCLE_COUNT` ×1,2 em "Muitos") |
| 🌋 Vulcanismo | Menos · **Padrão** · Mais vulcões · Terra vulcânica | `VOLCANISM_FREQUENCY`, `VOLCANO_MIN`, `VOLCANISM` (máx. 100), `VOLCANISM_RANGES` |
| ⛰️ Relevo | Mais planícies · **Padrão** · Montanhoso | `ELEVATION_FREQUENCY`, `PEAK_NUMBER_MIN`, `REGION_COUNTS:MOUNTAINS`, `ELEVATION_RANGES` |
| ⛏️ Minérios | Escassos · **Padrão** · Abundantes · Muito abundantes | `MINERAL_SCARCITY` (5000 / 2500 / 800 / 250) |
| ⚔️ Perigo | Tranquilo · **Padrão** · Perigoso · Brutal | megabestas, titãs, maldições, áreas malignas, `SAVAGERY_FREQUENCY`/`_RANGES`, gatilhos de ataque |
| 🕳️ Cavernas | Mais fechadas · **Padrão** · Mais abertas · Abertas e úmidas | `CAVERN_LAYER_OPENNESS_*`, `CAVERN_LAYER_PASSAGE_DENSITY_*`, `CAVERN_LAYER_WATER_MIN` |
| 🪨 Camadas de caverna | 1 · 2 · **3 (padrão)** | `CAVERN_LAYER_COUNT` |
| 📜 História | Curta (125) · Média (250) · Longa (550) · **Padrão (1050)** | `END_YEAR` |
| 🎒 Embarque | **Padrão** · Generoso (+50%) · Muito generoso (2x) | `EMBARK_POINTS` |

Valores num mundo médio padrão, para referência: rios 100 → 300 (mais) / 400 (muitos); vulcões exigidos 3 → 9 (mais) / 18 (terra vulcânica).

Cada token é alterado por **uma categoria só** (há teste para isso), então combinar categorias nunca multiplica o mesmo valor duas vezes.

### Notas sobre as categorias

- **Magma raso**: vulcões levam magma até a superfície. Para o **mar de magma** ficar mais perto, use **Camadas de caverna = 1 ou 2**: ele fica abaixo da última caverna. O perfil **Magma fácil** já combina isso. (Uma versão anterior mexia em `LEVELS_ABOVE_LAYER_1`, que o jogo mede em níveis Z. Isso aproxima a primeira *caverna* da superfície, não o magma, e foi removido.)
- **Cavernas fechadas**: o jogo **não tem parâmetro de altura** para as cavernas. O que existe é a *abertura* (tamanho das câmaras) e a *densidade de passagens* (quantas ligações). "Mais fechadas" usa abertura 0–20 e passagens 0–25: câmaras menores e menos entradas para murar. Para limitar ainda mais o acesso, use também **Camadas de caverna = 1**.
- **Rios e relevo**: rios nascem em terreno alto. Com "Mais planícies" o jogo pode criar menos rios do que o pedido, mas não rejeita o mundo por isso.
- **Se o jogo ficar rejeitando**: ative "Log world generation map rejects" nas opções do jogo, ou rode o [laboratório](#validação-no-jogo), para ver o motivo.

### Como os níveis foram calibrados (e por que não dão problema)

O DF 53 rejeita um mapa quando ele não cumpre algum mínimo. A lista completa, tirada das mensagens do executável, é: chuva, drenagem, selvageria, vulcanismo, elevação baixa/média/alta, **falta de picos**, **falta de bordas com oceano**, **falta de vulcões** e os biomas (montanha, oceano, geleira, tundra, pântano, deserto, floresta, campo, colinas).

Daí saem três regras que os níveis seguem:

1. **Não existe rejeição por falta de rios.** Por isso "Mais rios" mexe **só** em `RIVER_MINS`: nada ali pode fazer o mapa ser rejeitado. "Muitos rios" aumenta a erosão em só 20%, porque erosão demais desgasta montanhas e pode causar rejeição por falta de montanha ou de picos.
2. **Aumentar o que é gerado, não só o que é exigido.** Os `*_RANGES` e `REGION_COUNTS` são *mínimos*: subir esses números só aumenta a chance de rejeição. Os níveis mudam a **distribuição** (pesos de `*_FREQUENCY`). Quando a distribuição muda, os mínimos das faixas que encolhem são reduzidos na mesma proporção, para continuarem cabendo.
3. **Vulcões nascem em pontos com vulcanismo 100.** As mensagens do jogo dizem que é preciso ter tantos pontos de vulcanismo máximo quantos vulcões exigidos. "Mais vulcões" dá 3x mais peso ao quinto mais alto do vulcanismo (onde está o 100) e exige 9, não 15.

Selvageria 66+ impede civilizações ("High savagery (66+) blocks civilization placement"). "Perigoso" e "Brutal" aumentam a selvageria alta pela distribuição e mantêm cerca de metade do mapa abaixo de 66.

### Risco de rejeição (estimativa)

O painel de resumo mostra **Risco de rejeição: baixo / médio / alto**, calculado por [`WorldGenRiskEstimator`](../src/DwarfVolon.Core/WorldGen/WorldGenRiskEstimator.cs) sem gerar o mundo:

- Para elevação, chuva, drenagem, selvageria e vulcanismo, estima quantos quadrados caem nas faixas baixa/média/alta a partir da faixa (`ELEVATION` etc.) e dos pesos (`*_FREQUENCY`). Elevação usa 0–99 / 100–299 / 300–400 (valores das mensagens do jogo); os outros campos usam terços.
- Compara com os mínimos (`*_RANGES`, `REGION_COUNTS` de montanha e oceano). Esperado ≥ 1,25x o exigido é risco baixo; entre 1x e 1,25x é médio; abaixo de 1x é alto.
- Verifica se ainda sobra área com selvageria abaixo de 66 para civilizações.
- Vulcões: exigir vulcões com vulcanismo máximo abaixo de 100 é risco alto. Fora isso, compara o mínimo exigido com o padrão do jogo ajustado pela distribuição.

É um **modelo aproximado**: o mapa real tem ruído e erosão. Resultado com os níveis atuais na região média:

| Situação | Risco estimado |
|---|---|
| Todos os níveis de Rios, Minérios, Cavernas, Camadas, História e Embarque | baixo |
| Vulcanismo: Menos / Mais vulcões; Relevo: todos; Perigo: Tranquilo / Perigoso | baixo |
| Vulcanismo: Terra vulcânica | médio (exige 18 vulcões, 2x o equivalente ao padrão) |
| Perigo: Brutal (e o perfil Desafio) | médio (47% do mapa com selvageria < 66) |

Ajustes manuais na aba Avançado também atualizam o risco na hora.

### Validação no jogo

A estimativa não substitui gerar o mundo de verdade. O [laboratório de world gen](../tools/DwarfVolon.WorldGenLab/Program.cs) faz isso automaticamente usando a linha de comando do jogo (`Dwarf Fortress.exe -gen <id> <seed> "<título>"`):

```powershell
# com o Dwarf Fortress FECHADO
dotnet run --project tools/DwarfVolon.WorldGenLab -- --list            # mostra os casos, não roda nada
dotnet run --project tools/DwarfVolon.WorldGenLab --                   # padrão + níveis com risco de rejeição + perfis, 2 seeds
dotnet run --project tools/DwarfVolon.WorldGenLab -- --only rios --seeds 3
dotnet run --project tools/DwarfVolon.WorldGenLab -- --all             # todas as categorias
```

O que ele faz:

1. Grava um conjunto `DVLAB ...` por caso no `world_gen.txt`, com história de 25 anos (`--years`) para ir rápido. As rejeições acontecem antes da história.
2. Liga `LOG_MAP_REJECTS` num `prefs/d_init.txt` temporário, para o jogo gravar os motivos em `map_rejection_log.txt`.
3. Gera cada mundo com seeds fixas, mede o tempo e mata o processo após `--timeout` segundos (padrão 900).
4. Restaura `world_gen.txt` e `d_init.txt` como estavam e apaga só os mundos `region9001+` que ele criou (`--keep` mantém).
5. Salva um relatório Markdown em `%APPDATA%\DwarfVolon\lab\` com tempo, rejeições e motivos por caso.

Se um caso der muitas rejeições ou timeout, o motivo mais comum no relatório indica qual mínimo afrouxar.

### Perfis rápidos

Definidos em [`Modifiers/_perfis.json`](../src/DwarfVolon.Core/WorldGen/Modifiers/_perfis.json). Cada perfil diz o nível de algumas categorias; as que ele não cita voltam ao padrão.

| Perfil | Escolhas |
|---|---|
| Tudo padrão | tudo no padrão |
| Rios e vulcões | Rios: mais · Vulcanismo: mais |
| Magma fácil | Vulcanismo: mais vulcões · Camadas: 2 · Minérios: abundantes |
| Fortaleza segura | Perigo: tranquilo · Cavernas: fechadas · Camadas: 2 · Embarque: generoso |
| Iniciante | Perigo: tranquilo · Minérios: abundantes · Rios: mais · Embarque: generoso · História: média |
| Desafio | Perigo: brutal · Minérios: escassos · Rios: poucos · Cavernas: abertas |

O perfil que corresponde exatamente às escolhas atuais fica destacado.

### Formato de uma categoria

```jsonc
{
  "id": "rios",                  // único; uma categoria do usuário com o mesmo id substitui a embutida
  "name": "Rios",
  "icon": "🌊",
  "order": 10,                   // posição na tela
  "description": "Texto do card.",
  "options": [
    { "id": "padrao", "name": "Padrão", "isDefault": true, "description": "..." },
    {
      "id": "mais", "name": "Mais rios", "titleTag": "MAIS RIOS",   // titleTag entra no título sugerido
      "description": "...",
      "notes": ["Avisos mostrados com ⚠."],
      "operations": [
        { "token": "RIVER_MINS", "multiply": [3, 3] },
        { "token": "VOLCANISM", "set": [null, 100] },                 // null = não mexer naquela posição
        { "token": "VOLCANO_MIN", "multiply": [5], "min": [5], "max": [200] },
        { "token": "REGION_COUNTS", "key": "MOUNTAINS", "multiply": [1.5, null, null] },
        { "token": "END_YEAR", "add": [100] }
      ]
    }
  ]
}
```

- As listas são **posicionais** sobre os valores do token (sem a chave).
- Ordem de aplicação em cada posição: `set` → `multiply` → `add` → `min` → `max`. Resultados numéricos são arredondados e nunca ficam negativos.
- Se o token não existe na base, só um `set` completo (sem `null`) consegue criá-lo; senão a operação é ignorada e aparece um aviso.
- As categorias são aplicadas na ordem de `order`.

### Criar ou alterar categorias

1. Na aba Avançado, clique em **Pasta dos meus modificadores** (abre `%APPDATA%\DwarfVolon\modificadores`).
2. Crie um `.json` no formato acima. Copiar um dos [embutidos](../src/DwarfVolon.Core/WorldGen/Modifiers/) é o jeito mais rápido. Use o mesmo `id` de uma embutida para substituí-la.
3. Clique em **Recarregar modificadores**. Arquivos com erro aparecem na mensagem de status e não derrubam o app.

## Formato do arquivo do jogo

```
[WORLD_GEN]
	[TITLE:MEDIUM REGION]
	[DIM:129:129]
	[RIVER_MINS:100:100]
	[VOLCANO_MIN:3]
	[REGION_COUNTS:SWAMP:260:1:1]
	...
```

- Cada `[WORLD_GEN]` inicia um conjunto; `TITLE` é o nome mostrado no jogo.
- Tokens são `[NOME:arg:arg...]`. `[`, `]` e `:` são caracteres estruturais e não podem aparecer em títulos.
- `REGION_COUNTS` se repete, uma vez por bioma; o primeiro argumento (o bioma) é a **chave**.
- `PS_EL`, `PS_RF`... são dados do *World Painter* (uma linha por Y). O editor preserva, mas não mostra.
- A ordem dos tokens no catálogo ([`WorldGenTokenCatalog`](../src/DwarfVolon.Core/WorldGen/WorldGenTokenCatalog.cs)) é a mesma em que o jogo grava. Ela foi tirada do script `export-worldgen.lua` do DFHack.

### Como o editor preserva o arquivo

- O arquivo é lido como Latin-1, que mapeia cada byte para um caractere, então nenhum byte se perde.
- Cada linha guarda o texto original. Só linhas cujo token mudou são reescritas; o resto sai exatamente igual, inclusive comentários, indentação e quebra de linha (`\r\n` ou `\n`).
- Tokens novos entram na posição canônica (ex.: `NIGHTMARE_NUMBER` logo depois de `BOGEYMAN_NUMBER`).
- Há testes garantindo que "ler e salvar sem mudanças" gera texto idêntico.

## Os conjuntos-base embutidos

[`Resources/df047_default_world_gen.txt`](../src/DwarfVolon.Core/WorldGen/Resources/df047_default_world_gen.txt) contém os 12 conjuntos padrão que o DF 0.47 trazia em `data/init/world_gen.txt` (pocket, smaller, small, medium e large; região e ilha; e as versões "easy").

O DF 53 não traz mais esse arquivo; os padrões ficam dentro do executável. Os padrões da 0.47 continuam válidos, mas **não têm** alguns tokens novos (`NIGHTMARE_NUMBER`, `ALLOW_DIVINATION`, `ALLOW_NECROMANCER_*` etc.). Quando faltam, o jogo usa os valores padrão dele.

Para ter uma base 100% atual, rode no jogo, com um mundo carregado e DFHack ativo:

```
export-worldgen --title BASE DF53
```

O conjunto aparece como base na aba Avançado depois de **Recarregar arquivo do jogo**.

## Referências

- Script `export-worldgen` do DFHack (`hack/scripts/export-worldgen.lua` na instalação), usado como fonte da lista e da ordem dos tokens.
- [DF Wiki – Advanced world generation](https://dwarffortresswiki.org/index.php/Advanced_world_generation)
- [DF Wiki – World rejection](https://dwarffortresswiki.org/index.php/World_rejection)
