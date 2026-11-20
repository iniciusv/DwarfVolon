using DwarfVolon.Core.Embark;
using DwarfVolon.DFHack;

// Ferramenta de desenvolvimento para falar com o DFHack do jogo aberto.
//   dfhack-cli version
//   dfhack-cli run <comando> [args...]
//   dfhack-cli lua "<código>"
//   dfhack-cli lua-file <arquivo.lua>
//   dfhack-cli embark          (análise da tela de embarque em Markdown)

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length == 0)
{
    Console.WriteLine("uso: version | run <comando> [args...] | lua \"<código>\" | lua-file <arquivo> | embark");
    return 2;
}

await using var client = await DfHackClient.ConnectAsync();

switch (args[0])
{
    case "version":
        Console.WriteLine($"DFHack {await client.GetDfHackVersionAsync()} · DF {await client.GetDfVersionAsync()}");
        return 0;
    case "run":
        return Print(await client.RunCommandAsync(args[1], args.Skip(2)));
    case "lua":
        return Print(await client.RunLuaAsync(string.Join(' ', args.Skip(1))));
    case "lua-file":
        return Print(await client.RunLuaAsync(File.ReadAllText(args[1])));
    case "embark":
    {
        await using var session = new DfHackSession(() => DfHackEndpoint.Default);
        var snapshot = await new EmbarkReader(session).ReadAsync();
        Console.WriteLine(snapshot.State == EmbarkScreenState.Embark
            ? EmbarkMarkdown.Build(EmbarkAnalyzer.Analyze(snapshot))
            : EmbarkMarkdown.NotOnEmbarkScreen(snapshot.Screen));
        return 0;
    }
    default:
        Console.Error.WriteLine($"comando desconhecido: {args[0]}");
        return 2;
}

static int Print(CommandResult result)
{
    Console.Write(result.Output);
    if (!result.Success) Console.Error.WriteLine($"[{result.Code}]");
    return result.Success ? 0 : 1;
}
