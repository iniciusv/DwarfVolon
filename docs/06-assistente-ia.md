# 06 – Assistente de IA (planejado)

## Objetivo

Um chat dentro do app que entende o estado da fortaleza e consegue:

- responder perguntas ("quem está ocioso?", "temos comida para o inverno?");
- sugerir decisões ("faltam ferreiros; o Urist tem Armorsmith 8 e está carregando pedra");
- executar ações, **sempre com aprovação do jogador**.

## Provedor

**DeepSeek**, cuja API é compatível com a da OpenAI:

- Base URL: `https://api.deepseek.com`
- Modelos: `deepseek-chat` (rápido) e `deepseek-reasoner` (raciocínio)
- Suporta *function calling* (ferramentas)

Plano técnico: usar `Microsoft.Extensions.AI` (`IChatClient`) com o cliente OpenAI apontando para a base URL do DeepSeek. Assim o código das ferramentas não depende do provedor, e dá para trocar por outro (OpenAI, Claude, modelo local) só mudando a configuração.

## Ferramentas

As ferramentas da IA são **finas**: chamam os mesmos serviços do Core que a UI usa (ver [Arquitetura](02-arquitetura.md)).

| Ferramenta | Tipo | Aprovação |
|---|---|---|
| `get_fortress_summary` | leitura | automática |
| `list_dwarves(filter)` | leitura | automática |
| `get_dwarf(id)` | leitura | automática |
| `list_world_gen_modifiers` / `preview_world_gen(escolhas)` | leitura | automática |
| `assign_work_detail(id, detail, on)` | escrita | **cartão de aprovação** |
| `save_world_gen(...)` | escrita | **cartão de aprovação** |
| `run_dfhack_command(cmd)` | escrita | **aprovação + lista de permitidos** |

Na UI, uma ação de escrita aparece como um cartão: *"A IA quer: colocar Urist em Mining. [Aprovar] [Recusar]"*. Nada é executado sem clique.

## Contexto

- Não mandar dados brutos. Mandar resumos compactos em **Markdown** (tabelas do [módulo Markdown](07-markdown.md): só as colunas e linhas relevantes, contagens, alertas).
- Junto com os dados, mandar o **catálogo do que dá para fazer** (ex.: o catálogo de modificadores do world gen, com os `id`s).
- O resumo da fortaleza é gerado sob demanda pela ferramenta, não colado em toda mensagem.
- Guardar o histórico da conversa por save/fortaleza.

## Segurança e privacidade

- A chave de API fica em `%APPDATA%\DwarfVolon\settings.json` (ou em variável de ambiente `DEEPSEEK_API_KEY`) e **nunca** no repositório.
- `run_dfhack_command` tem lista de comandos permitidos; nada de `lua` arbitrário vindo do modelo.
- Mostrar ao usuário quanto contexto está sendo enviado.

## Integração com MCP (opcional)

O servidor [dfhack-mcp](https://www.npmjs.com/package/dfhack-mcp) serve de **referência** para o desenho das ferramentas (ele é somente leitura por padrão e libera escrita por uma flag). Não é dependência: colocar Node dentro do app só complicaria.

Mais para frente o próprio DwarfVolon pode **expor** as ferramentas como servidor MCP (há SDK oficial em C#), para que Claude Desktop e outros clientes usem a mesma camada.
