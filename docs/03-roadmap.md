# 03 – Roadmap

Legenda: ✅ pronto · 🚧 em andamento · ⏳ planejado

## Fase 0 – Fundação ✅

- ✅ Converter a solution de F# para C# ([decisão](decisoes.md#d1--c-em-vez-de-f))
- ✅ Separar `Core` (sem UI) do app Avalonia
- ✅ Projetos de teste (Core e view-models)
- ✅ Detectar a instalação do jogo pela Steam e salvar o caminho
- ✅ Documentação inicial (esta pasta)

## Fase 1 – Editor de world gen ✅

- ✅ Parser/escritor do `world_gen.txt` que preserva o arquivo byte a byte
- ✅ Catálogo dos tokens do DF 53.x com nomes e explicações em pt-BR
- ✅ Conjuntos padrão do jogo embutidos como base (pocket a large, região e ilha)
- ✅ Operações como "patches" multiplicativos (funcionam em qualquer tamanho de mundo)
- ✅ **Categorias de modificadores** com níveis (Rios, Vulcanismo, Relevo, Minérios, Perigo, Cavernas, Camadas de caverna, História, Embarque), calibradas para região média
- ✅ **Perfis rápidos** (Rios e vulcões, Magma fácil, Fortaleza segura, Iniciante, Desafio)
- ✅ Tela **Simples** (cards com níveis) e aba **Avançado** (todos os parâmetros, base, log, Markdown, texto gerado)
- ✅ Categorias do usuário em `%APPDATA%\DwarfVolon\modificadores` (podem substituir as embutidas)
- ✅ Salvar com backup e substituição pelo título

Próximos passos da fase:

- ✅ Revisão dos níveis contra as regras de rejeição do jogo; estimador de risco na UI e no Markdown
- ✅ Laboratório (`tools/DwarfVolon.WorldGenLab`) que gera os mundos no jogo e mede rejeições
- ⏳ Rodar o laboratório na região média e ajustar os números que derem muita rejeição
- ⏳ Calibrar os níveis para outros tamanhos (pequeno, grande) e ilhas
- ⏳ Importar como base o resultado do `export-worldgen` do DFHack (conjunto canônico do DF 53)
- ⏳ Remover/renomear conjuntos do `world_gen.txt` pela UI
- ⏳ Salvar as escolhas atuais como perfil do usuário
- ⏳ Mostrar tokens do catálogo que não estão na base (ex.: `NIGHTMARE_NUMBER` nos padrões 0.47) para poder adicioná-los
- ⏳ Calibrar o estimador de risco com os resultados do laboratório

## Fase 1.5 – Interface em módulos e Markdown ✅

- ✅ Janela com menu lateral (Gerador de mundo, Anões, Assistente, Configurações), pronta para novas páginas
- ✅ Módulo Markdown genérico: tabelas com colunas escolhidas, construtor, parser ([07 – Markdown](07-markdown.md))
- ✅ Renderizador de Markdown na UI e páginas feitas de Markdown
- ✅ Resumo do mundo e catálogo de modificadores em Markdown, com copiar
- ⏳ Componente "tabela com seletor de colunas" (base da tela de Anões)

## Fase 2 – Conexão com o DFHack (somente leitura) 🚧

Detalhes em [05 – Integração com DFHack](05-integracao-dfhack.md).

- ✅ Cliente RPC (porta 5000): handshake, `BindMethod`, `RunCommand`, versões; reconexão automática
- ✅ Scripts Lua próprios que devolvem JSON, enviados pelo comando `lua` (nada instalado no jogo)
- ✅ Ferramenta `tools/DwarfVolon.DfHackCli` para testar com o jogo aberto
- ⏳ Indicador de conexão na UI (desconectado / jogo sem fortaleza / fortaleza carregada)
- ⏳ Tela **Anões**: tabela Markdown com colunas escolhidas (profissão, habilidades, humor/estresse, ferimentos, trabalho atual); filtros como "ociosos" e "com habilidade X sem usar"
- ✅ Tela **Embarque**: anões, atributos, habilidades, nota por função e sugestão de funções e níveis ([08 – Embarque](08-embarque.md))
- ⏳ Validar o Embarque na tela real do jogo (custo dos níveis x pontos de embarque)
- ⏳ Embarque: preferências dos anões, itens e animais

## Fase 3 – Ações na fortaleza ⏳

- ⏳ Atribuir funções / *work details* (com confirmação)
- ⏳ Ações em lote ("os 3 melhores mineradores em Mining")
- ⏳ Histórico das ações feitas pelo app, com desfazer quando possível

## Fase 4 – Assistente de IA (DeepSeek) ⏳

Detalhes em [06 – Assistente de IA](06-assistente-ia.md).

- ⏳ Chat na UI com ferramentas de leitura automáticas e de escrita com aprovação
- ⏳ Resumo compacto do estado da fortaleza como contexto
- ⏳ Configuração da chave de API fora do repositório

## Fase 5 – Agendamento e automações ⏳

- ⏳ Regras "quando X, faça Y" avaliadas por consulta periódica
- ⏳ Revisão periódica pela IA ("a cada estação, sugira ajustes")
- ⏳ (Opcional) expor as ferramentas como servidor MCP para outros clientes
