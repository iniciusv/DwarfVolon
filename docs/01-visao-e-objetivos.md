# 01 – Visão e objetivos

## Problema

Várias funções do Dwarf Fortress são difíceis de usar dentro do próprio jogo:

- **Geração de mundo avançada**: são cerca de 100 parâmetros numéricos numa tela sem explicação. Para ter "mais rios e vulcões" é preciso saber quais tokens mexer e em quanto, e errar a mão faz o jogo rejeitar mapas sem parar.
- **Gestão dos anões**: ver habilidades, humor, ferimentos e trabalho de todo mundo e redistribuir funções exige navegar por muitas telas.
- **Acompanhamento da fortaleza**: informações importantes (estoques, ameaças, quem está ocioso) ficam espalhadas.

## Objetivo

Um app de apoio, rodando ao lado do jogo, que:

1. **Edita a geração de mundo** escolhendo níveis por categoria ("Mais rios", "Magma perto da superfície", "Cavernas fechadas"...) e perfis prontos, sem mexer em parâmetro; o editor completo fica na aba Avançado. *(Fase 1, pronta.)*
2. **Mostra uma visão geral dos anões** e facilita atribuir funções. *(Fases 2 e 3, via DFHack.)*
3. **Oferece um chat com IA (DeepSeek)** que consulta o estado da fortaleza e sugere ou executa ações, sempre com aprovação do jogador. *(Fase 4.)*
4. **Agenda tarefas**: regras do tipo "quando X acontecer, faça Y" ou revisões periódicas feitas pela IA. *(Fase 5.)*

## Princípios

- **Nunca corromper dados do jogador.** Toda escrita em arquivo do jogo faz backup antes, e o parser preserva tudo o que não entende.
- **Leitura é livre; escrita pede confirmação.** Vale para a UI e, principalmente, para a IA.
- **Uma camada de serviços para tudo.** O que um botão faz, a IA também consegue fazer, e vice-versa (ver [Arquitetura](02-arquitetura.md)).
- **Funcionar sem o jogo aberto sempre que possível.** O editor de world gen não depende do DFHack.

## Fora do escopo (por enquanto)

- Substituir a interface do jogo ou desenhar o mapa.
- Mods de raws (criaturas, materiais).
- Suporte a versões anteriores à v50 (pré-Steam).
