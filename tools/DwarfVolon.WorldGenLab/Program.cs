// Laboratório de geração de mundo: valida os modificadores no jogo de verdade.
//
// Para cada caso (padrão, cada nível de categoria, cada perfil) grava um conjunto "DVLAB ..." no
// world_gen.txt, roda `Dwarf Fortress.exe -gen <id> <seed> "<título>"`, mede o tempo e conta as
// rejeições de mapa (liga LOG_MAP_REJECTS temporariamente). No fim restaura world_gen.txt e d_init.txt
// e apaga só os mundos de teste criados aqui. O relatório sai em Markdown.
//
// Uso: dotnet run --project tools/DwarfVolon.WorldGenLab -- [--all] [--only rios,vulcanismo] [--seeds 2]
//                                                           [--timeout 900] [--years 25] [--keep] [--list]

using System.Diagnostics;
using System.Text.RegularExpressions;
using DwarfVolon.Core.DwarfFortress;
using DwarfVolon.Core.Markdown;
using DwarfVolon.Core.Settings;
using DwarfVolon.Core.WorldGen;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var options = LabOptions.Parse(args);
var installation = FindInstallation();
if (installation is null)
{
    Console.Error.WriteLine("Dwarf Fortress não encontrado. Configure a pasta no DwarfVolon primeiro.");
    return 1;
}

var cases = BuildCases(options);
Console.WriteLine($"Dwarf Fortress: {installation.InstallDirectory}");
Console.WriteLine($"world_gen.txt:  {installation.WorldGenFilePath}");
Console.WriteLine($"{cases.Count} casos x {options.Seeds.Count} seeds = {cases.Count * options.Seeds.Count} mundos (história de {options.Years} anos).");
foreach (var c in cases) Console.WriteLine($"  - {c.Title}");
if (options.ListOnly) return 0;

if (Process.GetProcessesByName("Dwarf Fortress").Length > 0)
{
    Console.Error.WriteLine("Feche o Dwarf Fortress antes de rodar o laboratório.");
    return 2;
}

var lab = new Lab(installation, options);
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); Console.WriteLine("Cancelando..."); };

List<LabResult> results;
try
{
    lab.Prepare(cases);
    results = lab.Run(cases, cancel.Token);
}
finally
{
    lab.Restore();
}

var report = Report(results, options, installation);
var reportPath = Path.Combine(AppPaths.AppDataDirectory, "lab", $"worldgen-{DateTime.Now:yyyyMMdd-HHmmss}.md");
Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
File.WriteAllText(reportPath, report);
Console.WriteLine();
Console.WriteLine(report);
Console.WriteLine($"Relatório salvo em {reportPath}");
return 0;

static DwarfFortressInstallation? FindInstallation()
{
    var configured = new SettingsStore().Load().DwarfFortressDirectory;
    if (configured is not null && new DwarfFortressInstallation(configured) is { IsValid: true } inst) return inst;
    return DwarfFortressLocator.Detect();
}

static List<LabCase> BuildCases(LabOptions options)
{
    var categories = ModifierLibrary.LoadBuiltInCategories();
    var baseSet = BuiltInTemplates.Load().FindByTitle(BuiltInTemplates.DefaultTitle)!;
    var cases = new List<LabCase>();

    LabCase Make(string name, IEnumerable<(ModifierCategory Category, ModifierOption Option)> picks)
    {
        var set = baseSet.Clone();
        PresetApplier.Apply(set, picks.Select(p => p.Option.ToPreset(p.Category)));
        set.Set("END_YEAR", options.Years.ToString());
        set.Title = $"DVLAB {name}";
        var risk = WorldGenRiskEstimator.Overall(WorldGenRiskEstimator.Estimate(set));
        return new LabCase(set.Title!, name, set, risk);
    }

    cases.Add(Make("PADRAO", []));

    // Por padrão, só as categorias que mexem em parâmetros que podem causar rejeição.
    string[] risky = ["rios", "vulcanismo", "relevo", "perigo"];
    foreach (var category in categories)
    {
        var selected = options.Only is { Count: > 0 } only ? only.Contains(category.Id) : options.All || risky.Contains(category.Id);
        if (!selected) continue;
        foreach (var option in category.Options.Where(o => !o.IsDefault))
            cases.Add(Make($"{category.Id} {option.Id}".ToUpperInvariant(), [(category, option)]));
    }

    if (options.Only is not { Count: > 0 })
    {
        foreach (var profile in ModifierLibrary.LoadBuiltInProfiles().Where(p => p.Selections.Count > 0))
        {
            var picks = profile.Selections.Select(s =>
            {
                var c = categories.Single(x => x.Id == s.Key);
                return (c, c.FindOption(s.Value)!);
            });
            cases.Add(Make($"PERFIL {profile.Id}".ToUpperInvariant(), picks));
        }
    }

    return cases;
}

static string Report(List<LabResult> results, LabOptions options, DwarfFortressInstallation installation)
{
    var md = new MarkdownBuilder()
        .Heading(1, "Laboratório de world gen")
        .KeyValues(
        [
            ("Data", DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
            ("Base", $"{BuiltInTemplates.DefaultTitle} (129×129)"),
            ("História", $"{options.Years} anos (encurtada para o teste; as rejeições acontecem antes da história)"),
            ("Seeds", string.Join(", ", options.Seeds)),
            ("Log de rejeições", results.Any(r => r.RejectionLogFound) ? "lido" : "não encontrado (contagem de rejeições indisponível)"),
        ]);

    md.Table(["Caso", "Risco estimado", "OK", "Tempo médio", "Rejeições", "Motivos mais comuns"],
        results.GroupBy(r => r.Case).Select(g =>
        {
            var ok = g.Count(r => r.Status == LabStatus.Ok);
            var reasons = g.SelectMany(r => r.Rejections).GroupBy(x => x.Key).Select(x => (x.Key, Count: x.Sum(y => y.Value)))
                           .OrderByDescending(x => x.Count).Take(3).Select(x => $"{x.Key} ({x.Count})");
            var statuses = g.Where(r => r.Status != LabStatus.Ok).Select(r => $"{r.Seed}: {r.Status}");
            return (IReadOnlyList<object?>)
            [
                g.Key.Name, WorldGenRiskEstimator.Describe(g.Key.EstimatedRisk), $"{ok}/{g.Count()}" + (statuses.Any() ? $" ({string.Join(", ", statuses)})" : ""),
                $"{g.Average(r => r.Elapsed.TotalSeconds):0}s", g.Sum(r => r.Rejections.Values.Sum()), string.Join(", ", reasons),
            ];
        }),
        [MdAlign.Left, MdAlign.Left, MdAlign.Center, MdAlign.Right, MdAlign.Right, MdAlign.Left]);

    md.Paragraph("**OK** = o jogo terminou a geração e criou a pasta do mundo. **Timeout** = passou do limite (geralmente rejeitando sem parar ou parado num aviso).");
    return md.ToString();
}

sealed record LabCase(string Title, string Name, WorldGenParameterSet Set, RiskLevel EstimatedRisk);

enum LabStatus { Ok, Timeout, Failed, Canceled }

sealed record LabResult(LabCase Case, string Seed, LabStatus Status, TimeSpan Elapsed, Dictionary<string, int> Rejections, bool RejectionLogFound);

sealed class LabOptions
{
    public bool All { get; private set; }
    public HashSet<string>? Only { get; private set; }
    public List<string> Seeds { get; private set; } = ["1001", "2002"];
    public int TimeoutSeconds { get; private set; } = 900;
    public int Years { get; private set; } = 25;
    public bool Keep { get; private set; }
    public bool ListOnly { get; private set; }

    public static LabOptions Parse(string[] args)
    {
        var o = new LabOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--all": o.All = true; break;
                case "--keep": o.Keep = true; break;
                case "--list": o.ListOnly = true; break;
                case "--only": o.Only = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(); break;
                case "--seeds":
                    var n = int.Parse(args[++i]);
                    o.Seeds = Enumerable.Range(1, n).Select(k => (k * 1001).ToString()).ToList();
                    break;
                case "--timeout": o.TimeoutSeconds = int.Parse(args[++i]); break;
                case "--years": o.Years = int.Parse(args[++i]); break;
                default: throw new ArgumentException($"Opção desconhecida: {args[i]}");
            }
        }
        return o;
    }
}

sealed partial class Lab(DwarfFortressInstallation installation, LabOptions options)
{
    private const int FirstWorldId = 9001;
    private string? _worldGenBackup;
    private bool _worldGenExisted;
    private string? _dInitBackup;
    private bool _dInitExisted;
    private bool _prepared;
    private readonly List<string> _createdPaths = [];

    private string DInitPath => Path.Combine(installation.PrefsDirectory, "d_init.txt");

    /// <summary>Lugares onde o jogo pode gravar mundos e logs (instalação e dados do usuário).</summary>
    private IEnumerable<string> Roots => new[] { installation.InstallDirectory, installation.UserDataDirectory }.Distinct(StringComparer.OrdinalIgnoreCase);

    public void Prepare(IReadOnlyList<LabCase> cases)
    {
        Directory.CreateDirectory(installation.PrefsDirectory);

        _worldGenExisted = File.Exists(installation.WorldGenFilePath);
        _worldGenBackup = _worldGenExisted ? File.ReadAllText(installation.WorldGenFilePath, WorldGenDocument.FileEncoding) : null;
        var document = _worldGenExisted ? WorldGenDocument.Load(installation.WorldGenFilePath) : WorldGenDocument.CreateEmpty();
        foreach (var c in cases) document.Upsert(c.Set);
        document.Save(installation.WorldGenFilePath);

        _dInitExisted = File.Exists(DInitPath);
        _dInitBackup = _dInitExisted ? File.ReadAllText(DInitPath) : null;
        var dInit = _dInitBackup ?? File.ReadAllText(Path.Combine(installation.InstallDirectory, "data", "init", "d_init_default.txt"));
        dInit = LogRejectsRegex().IsMatch(dInit)
            ? LogRejectsRegex().Replace(dInit, "[LOG_MAP_REJECTS:YES]")
            : dInit + Environment.NewLine + "[LOG_MAP_REJECTS:YES]" + Environment.NewLine;
        File.WriteAllText(DInitPath, dInit);
        _prepared = true;
    }

    public List<LabResult> Run(IReadOnlyList<LabCase> cases, CancellationToken cancel)
    {
        var results = new List<LabResult>();
        var worldId = FirstWorldId;
        var total = cases.Count * options.Seeds.Count;

        foreach (var c in cases)
        foreach (var seed in options.Seeds)
        {
            if (cancel.IsCancellationRequested) return results;
            while (WorldExists(worldId)) worldId++;

            Console.Write($"[{results.Count + 1}/{total}] {c.Name} seed {seed} ... ");
            var result = RunOne(c, seed, worldId, cancel);
            results.Add(result);
            Console.WriteLine($"{result.Status} em {result.Elapsed.TotalSeconds:0}s, {result.Rejections.Values.Sum()} rejeições");
            worldId++;
        }
        return results;
    }

    private LabResult RunOne(LabCase c, string seed, int worldId, CancellationToken cancel)
    {
        var before = Snapshot();
        var logs = RejectionLogs().ToDictionary(p => p, p => new FileInfo(p).Length);

        var start = new ProcessStartInfo(installation.ExecutablePath!)
        {
            WorkingDirectory = installation.InstallDirectory,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-gen");
        start.ArgumentList.Add(worldId.ToString());
        start.ArgumentList.Add(seed);
        start.ArgumentList.Add(c.Title);

        var watch = Stopwatch.StartNew();
        var status = LabStatus.Failed;
        using (var process = Process.Start(start)!)
        {
            var deadline = TimeSpan.FromSeconds(options.TimeoutSeconds);
            while (!process.WaitForExit(500))
            {
                if (cancel.IsCancellationRequested) { process.Kill(entireProcessTree: true); status = LabStatus.Canceled; break; }
                if (watch.Elapsed > deadline) { process.Kill(entireProcessTree: true); status = LabStatus.Timeout; break; }
            }
            process.WaitForExit();
        }
        watch.Stop();

        var created = Snapshot().Except(before, StringComparer.OrdinalIgnoreCase)
            .Where(p => Path.GetFileName(p).StartsWith($"region{worldId}", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (status == LabStatus.Failed && created.Any(Directory.Exists)) status = LabStatus.Ok;
        _createdPaths.AddRange(created);

        var rejections = new Dictionary<string, int>();
        var logFound = false;
        foreach (var log in RejectionLogs())
        {
            logFound = true;
            var offset = logs.GetValueOrDefault(log);
            using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (offset > stream.Length) offset = 0;
            stream.Seek(offset, SeekOrigin.Begin);
            foreach (Match m in RejectionRegex().Matches(new StreamReader(stream).ReadToEnd()))
                rejections[m.Value.Trim()] = rejections.GetValueOrDefault(m.Value.Trim()) + 1;
        }

        if (!options.Keep) DeleteCreated(created);
        return new LabResult(c, seed, status, watch.Elapsed, rejections, logFound);
    }

    public void Restore()
    {
        if (!_prepared) return;
        if (_worldGenExisted) File.WriteAllText(installation.WorldGenFilePath, _worldGenBackup!, WorldGenDocument.FileEncoding);
        else File.Delete(installation.WorldGenFilePath);

        if (_dInitExisted) File.WriteAllText(DInitPath, _dInitBackup!);
        else File.Delete(DInitPath);
        Console.WriteLine("world_gen.txt e d_init.txt restaurados.");
    }

    private bool WorldExists(int id) =>
        Roots.Any(root => Directory.Exists(Path.Combine(root, "save", $"region{id}")));

    /// <summary>Entradas de primeiro nível nas pastas onde o jogo grava mundos e exportações.</summary>
    private HashSet<string> Snapshot()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in Roots)
        foreach (var dir in new[] { root, Path.Combine(root, "save") })
            if (Directory.Exists(dir))
                paths.UnionWith(Directory.EnumerateFileSystemEntries(dir));
        return paths;
    }

    private IEnumerable<string> RejectionLogs() =>
        Roots.Select(r => Path.Combine(r, "map_rejection_log.txt")).Where(File.Exists);

    private static void DeleteCreated(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                else if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Não consegui apagar {path}: {ex.Message}");
            }
        }
    }

    [GeneratedRegex(@"\[LOG_MAP_REJECTS:[A-Z]+\]")]
    private static partial Regex LogRejectsRegex();

    [GeneratedRegex(@"(?:[A-Z]+ )*(?:REJECTION|LACK OF [A-Z ]+?)(?=\s|$)", RegexOptions.Multiline)]
    private static partial Regex RejectionRegex();
}
