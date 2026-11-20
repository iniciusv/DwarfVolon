# Ideias (backlog)

Ideias soltas, ainda sem fase definida. Quando uma for planejada, ela vai para o [roadmap](03-roadmap.md).

## World gen

- Gerar mundos pela linha de comando do jogo (`command line.txt` do DF descreve `-gen`) direto do app, em lote, e listar quais deram certo.
- "Aleatorizar com limites": sortear valores dentro de faixas seguras para ter mundos variados.
- Prévia aproximada (mapa de calor) de elevação, chuva e vulcanismo a partir dos parâmetros.
- Ler o log de rejeição do jogo e sugerir qual parâmetro afrouxar.
- Compartilhar categorias e perfis por arquivo ou link.
- Editor para os dados do World Painter (`PS_*`): pintar continentes e rios.

## Fortaleza (com DFHack)

- Painel de alertas: anões com estresse alto, feridos sem tratamento, comida e bebida baixas, invasões.
- "Quem sabe fazer X?": busca por habilidade com ranking.
- Sugestão de work details com base nas habilidades.
- Linha do tempo de eventos (mortes, artefatos, chegadas de migrantes).
- Exportar o estado da fortaleza para planilha/JSON.

## IA

- "Diário da fortaleza": a IA escreve um resumo narrativo de cada estação.
- Modo conselheiro: só sugere, nunca executa.
- Perguntas sobre a wiki do DF com o contexto da fortaleza atual.
- Usar modelos locais (Ollama) como alternativa ao DeepSeek.

## Infra

- Instalador / publicação single-file.
- Atualização automática dos scripts Lua do app na pasta do DFHack.
- Telemetria local (só no PC) de quais níveis geram mais rejeição.
