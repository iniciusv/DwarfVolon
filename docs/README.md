# Documentação do DwarfVolon

DwarfVolon é um app desktop (C# + Avalonia) que facilita tarefas do Dwarf Fortress que são trabalhosas dentro do jogo: configurar a geração de mundo, ver e gerenciar os anões e, no futuro, conversar com uma IA que consulta e age na fortaleza via DFHack.

## Índice

| Documento | Conteúdo |
|---|---|
| [01 – Visão e objetivos](01-visao-e-objetivos.md) | O que o projeto quer resolver e o que está fora do escopo |
| [02 – Arquitetura](02-arquitetura.md) | Projetos da solution, camadas e convenções |
| [03 – Roadmap](03-roadmap.md) | Fases de desenvolvimento e status de cada uma |
| [04 – Gerador de mundo](04-world-gen.md) | Categorias de modificadores, perfis, aba Avançado e o formato do `world_gen.txt` |
| [05 – Integração com DFHack](05-integracao-dfhack.md) | Conversa com o jogo em execução (RPC na porta 5000, scripts Lua → JSON) |
| [06 – Assistente de IA](06-assistente-ia.md) | Plano para o chat com DeepSeek e o uso de ferramentas |
| [07 – Módulo Markdown](07-markdown.md) | Tabelas e documentos Markdown compartilhados entre telas e IA |
| [08 – Embarque](08-embarque.md) | Análise dos anões iniciais e sugestão de funções e pontos |
| [Decisões](decisoes.md) | Registro das decisões técnicas e do motivo de cada uma |
| [Ideias](ideias.md) | Backlog de ideias ainda não planejadas |

## Começando

Requisitos: .NET SDK 10 (fixado em [`global.json`](../global.json)) e Dwarf Fortress v50+ (Steam). O DFHack só é necessário para as telas que leem o jogo aberto (ex.: Embarque).

```powershell
dotnet build DwarfVolon.slnx
dotnet test DwarfVolon.slnx
dotnet run --project src/DwarfVolon
```

Na primeira execução o app procura o jogo nas bibliotecas da Steam e salva o caminho em `%APPDATA%\DwarfVolon\settings.json`. Se não achar, informe a pasta na aba **Configurações**.

## Estrutura do repositório

```
DwarfVolon.slnx
global.json
docs/                         ← você está aqui
src/
  DwarfVolon.Core/            ← lógica sem UI (world gen, embarque, Markdown, detecção do jogo, configurações)
  DwarfVolon.DFHack/          ← cliente RPC do DFHack e scripts Lua
  DwarfVolon/                 ← app Avalonia (menu lateral, páginas, views e view-models)
tools/
  DwarfVolon.WorldGenLab/     ← gera mundos de teste no jogo e mede rejeições
  DwarfVolon.DfHackCli/       ← fala com o DFHack do jogo aberto (version, run, lua, embark)
tests/
  DwarfVolon.Core.Tests/      ← testes do Core
  DwarfVolon.Tests/           ← testes dos view-models
```
