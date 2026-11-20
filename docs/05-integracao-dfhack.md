# 05 – Integração com DFHack

## Como o DFHack expõe o jogo

O DFHack tem um servidor RPC embutido:

- **Transporte**: TCP em `127.0.0.1:5000`. A porta é configurada em `dfhack-config/remote-server.json`, e a variável `DFHACK_PORT` tem prioridade. Conexões remotas ficam desligadas por padrão; manter assim.
- **Protocolo**: mensagens protobuf com um cabeçalho simples.
  1. Handshake: o cliente envia `"DFHack?\n"` + versão (int32), e o servidor responde `"DFHack!\n"` + versão.
  2. Mensagens: cabeçalho `{ int16 id, int16 padding, int32 size }` + corpo protobuf.
  3. `BindMethod` (id 0) liga um método pelo nome (`method`, `plugin`, tipos de entrada/saída) e devolve um id.
  4. `RunCommand` (id 1) executa um comando do console do DFHack. É o que o `dfhack-run` usa.
- **Definições**: os `.proto` estão no repositório do DFHack (`library/proto/` e `plugins/remotefortressreader/proto/`).

Referências de clientes existentes:

- [Documentação oficial – Remote](https://dfhack.readthedocs.io/en/latest/docs/dev/Remote.html)
- [dfhack-remote (Rust)](https://github.com/plule/dfhack-remote): implementação clara do enquadramento das mensagens
- [dfhackrpc (Go)](https://pkg.go.dev/github.com/BenLubar/dfhackrpc)
- [dfhack-mcp (Node)](https://www.npmjs.com/package/dfhack-mcp): servidor MCP com ferramentas "semânticas"; bom para ver quais ferramentas fazem sentido para uma IA

Detalhes que só aparecem na prática:

- Respostas de falha (`id = -2`) não têm corpo: o campo de tamanho carrega o código do erro (`command_result`).
- A saída de texto de um comando chega em várias mensagens `CoreTextNotification` (`id = -3`) antes do resultado (`id = -1`).
- O texto vem em UTF-8 quando o script converte com `dfhack.df2utf` (nomes do jogo são CP437).

## Estratégia: "Lua → JSON" ✅

Em vez de criar um método protobuf para cada funcionalidade:

1. Scripts Lua próprios leem o estado do jogo e imprimem **uma linha de JSON** (módulo `json` do DFHack).
2. Os scripts ficam embutidos no app e vão inteiros como argumento do comando `lua` (`RunCommand("lua", [código])`). Nada é instalado na pasta do jogo ([D13](decisoes.md#d13--scripts-lua-enviados-pelo-comando-lua-sem-instalar-nada-no-jogo)).
3. O C# desserializa o JSON.

Regras para os scripts:

- Sempre devolver `{"ok": true, ...}` ou `{"ok": false, "error": "..."}`. O comando `lua` responde sucesso mesmo quando o código dá erro.
- Só números inteiros: o encoder segue o locale do jogo e pode escrever `1,5`.
- Tabelas vazias viram `[]` (o C# aceita `[]` onde espera objeto).
- O ambiente do comando `lua` persiste entre chamadas. Os scripts usam `local` e só leem globais de propósito (ex.: `DWARFVOLON_SCREEN`, para testar com uma tela montada à mão).

O protobuf gerado (`Grpc.Tools`) fica para o que for muito volumoso ou frequente (ex.: mapa, via RemoteFortressReader).

## Projetos

```
DwarfVolon.DFHack         (C#)  sem dependências além do Core
  Protocol/Proto.cs             protobuf mínimo escrito à mão (D12)
  DfHackClient.cs               conexão, handshake, BindMethod, RunCommand, RunLuaAsync, versões
  DfHackSession.cs              DfHackEndpoint (porta) + sessão que reconecta sozinha
  EmbarkReader.cs               roda Scripts/embark.lua e devolve EmbarkSnapshot
  Scripts/*.lua                 scripts embutidos
tools/DwarfVolon.DfHackCli      linha de comando: version | run | lua | lua-file | embark
```

A porta segue a regra do DFHack: `DFHACK_PORT` > `dfhack-config/remote-server.json` da instalação > 5000.

Interface pensada para os serviços:

```csharp
public interface IFortressService
{
    Task<FortressStatus> GetStatusAsync(CancellationToken ct);            // conectado? fortaleza carregada?
    Task<IReadOnlyList<DwarfSummary>> ListDwarvesAsync(CancellationToken ct);
    Task<DwarfDetails> GetDwarfAsync(int unitId, CancellationToken ct);
    Task AssignWorkDetailAsync(int unitId, string workDetail, bool enabled, CancellationToken ct); // escrita
}
```

## Cuidados conhecidos

- **Labors x work details**: desde a v50 o jogo controla trabalhos por *work details*. Mexer em labors individuais pode ser sobrescrito pelo jogo, então a Fase 3 deve agir sobre work details ou validar o comportamento antes.
- **Pausa e suspensão**: leituras grandes devem rodar com o core suspenso (o próprio `RunCommand` cuida disso) e o mais rápido possível, para não travar o jogo.
- **Versões**: o DFHack é atualizado a cada versão do DF. O app deve mostrar a versão do DF e do DFHack (`GetVersion`, `GetDFVersion`) e avisar se não reconhecer.
- **Jogo fechado**: toda a UI precisa funcionar (com aviso) quando não houver conexão.
