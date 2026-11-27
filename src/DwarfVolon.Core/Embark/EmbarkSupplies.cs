using DwarfVolon.Core.Markdown;

namespace DwarfVolon.Core.Embark;

public enum SupplyStatus
{
    Ok,
    Warning,
    Missing,
    Tip,
}

/// <summary>Um item da conferência de itens e animais.</summary>
public sealed record SupplyCheck(SupplyStatus Status, string Topic, string Text)
{
    public string Icon => Status switch
    {
        SupplyStatus.Ok => "✓",
        SupplyStatus.Warning => "⚠️",
        SupplyStatus.Missing => "✗",
        _ => "💡",
    };
}

/// <summary>
/// Itens e animais do embarque: conferência (o básico para o primeiro ano, ligado às funções decididas)
/// e o Markdown mostrado na subaba e mandado para a IA. Só lê: compras são feitas na tela do jogo.
/// </summary>
public static class EmbarkSupplies
{
    /// <summary>Comida que se come sem preparar (o jogo cozinha ou come direto).</summary>
    private static readonly HashSet<string> FoodTypes = ["MEAT", "FISH", "PLANT", "CHEESE", "PLANT_GROWTH", "EGG", "FOOD"];

    // Referências aproximadas por anão (o embarque padrão do jogo leva ~9 bebidas e ~6 comidas por anão
    // para 7 anões: dá até as primeiras colheitas, se alguém plantar e fazer bebida logo).
    private const int DrinkOkPerDwarf = 8, DrinkLowPerDwarf = 4;
    private const int FoodOkPerDwarf = 6, FoodLowPerDwarf = 3;

    public static int Count(EmbarkSnapshot s, Func<EmbarkItem, bool> match) => s.Items.Where(match).Sum(i => i.Stack);

    /// <summary>Quantos anões têm a função no plano; sem nenhuma função decidida, a quantidade do catálogo.</summary>
    private static int RoleCount(EmbarkAnalysis analysis, EmbarkPlan? plan, string roleId, out bool fromPlan)
    {
        var s = analysis.Snapshot;
        fromPlan = plan is not null && s.Dwarves.Any(d => plan.RolesOf(d).Count > 0);
        if (fromPlan) return s.Dwarves.Count(d => plan!.RolesOf(d).Contains(roleId));
        return analysis.Roles.FirstOrDefault(r => r.Id == roleId)?.Count ?? 0;
    }

    public static IReadOnlyList<SupplyCheck> Check(EmbarkAnalysis analysis, EmbarkPlan? plan = null)
    {
        var s = analysis.Snapshot;
        var checks = new List<SupplyCheck>();
        var dwarves = Math.Max(1, s.Dwarves.Count);

        // Picaretas: uma por minerador.
        var miners = Math.Max(1, RoleCount(analysis, plan, "minerador", out var minersFromPlan));
        var picks = Count(s, i => i.Subtype == "ITEM_WEAPON_PICK");
        var minersText = minersFromPlan ? $"{miners} mineradores no plano" : $"{miners} mineradores (sugestão)";
        checks.Add(picks >= miners
            ? new(SupplyStatus.Ok, "Picaretas", $"{picks} para {minersText}.")
            : new(picks == 0 ? SupplyStatus.Missing : SupplyStatus.Warning, "Picaretas",
                $"{picks} para {minersText}: sem picareta ninguém cava. Compre mais {miners - picks}."));

        // Machados: lenhador corta árvores com machado; o guarda luta com um.
        var guards = RoleCount(analysis, plan, "guarda", out _);
        var axesNeeded = 1 + guards;
        var axes = Count(s, i => i.Type == "WEAPON" && i.Subtype.Contains("_AXE"));
        checks.Add(axes >= axesNeeded
            ? new(SupplyStatus.Ok, "Machados", $"{axes} (lenhador{(guards > 0 ? $" + {guards} guarda" : "")}).")
            : new(axes == 0 ? SupplyStatus.Missing : SupplyStatus.Warning, "Machados",
                $"{axes} de {axesNeeded}: o lenhador precisa de machado para cortar árvores{(guards > 0 ? " e o guarda, para lutar" : "")}."));

        // Bigorna: a forja precisa de uma, e fazer outra exige ferro e uma forja.
        var anvils = Count(s, i => i.Type == "ANVIL");
        checks.Add(anvils > 0
            ? new(SupplyStatus.Ok, "Bigorna", $"{anvils} (para a forja).")
            : new(SupplyStatus.Warning, "Bigorna", "Nenhuma: sem bigorna não dá para montar forja (armas, armaduras, ferramentas de metal)."));

        // Sementes de plump helmet: a base da agricultura no subsolo (comida e bebida).
        var plump = Count(s, i => i.Type == "SEEDS" && i.Material.Contains("MUSHROOM_HELMET_PLUMP"));
        var seeds = Count(s, i => i.Type == "SEEDS");
        checks.Add(plump > 0
            ? new(SupplyStatus.Ok, "Sementes", $"{seeds} sementes, {plump} de plump helmet (plantio no subsolo, comida e bebida).")
            : new(SupplyStatus.Missing, "Sementes", seeds > 0
                ? $"{seeds} sementes, mas nenhuma de plump helmet: é a planta mais fácil do subsolo."
                : "Nenhuma semente: sem plantar, a comida e a bebida acabam."));

        // Bebida e comida, por anão.
        var drink = Count(s, i => i.Type == "DRINK");
        checks.Add(PerDwarf("Bebida", drink, dwarves, DrinkOkPerDwarf, DrinkLowPerDwarf,
            "anões sem álcool ficam lentos e infelizes; um cervejeiro precisa de plantas para fazer mais"));
        var food = Count(s, i => FoodTypes.Contains(i.Type));
        checks.Add(PerDwarf("Comida", food, dwarves, FoodOkPerDwarf, FoodLowPerDwarf,
            "dura até as primeiras colheitas"));

        // Médico: linha e tecido para suturar e enfaixar.
        if (RoleCount(analysis, plan, "medico", out var medicFromPlan) > 0 && medicFromPlan)
        {
            var thread = Count(s, i => i.Type == "THREAD");
            var cloth = Count(s, i => i.Type == "CLOTH");
            checks.Add(thread > 0 && cloth > 0
                ? new(SupplyStatus.Ok, "Médico", $"{thread} linhas e {cloth} tecidos para suturar e enfaixar.")
                : new(SupplyStatus.Warning, "Médico", $"Há médico no plano, mas {thread} linhas e {cloth} tecidos: ele precisa dos dois."));
        }

        checks.AddRange(CheckPets(s));
        return checks;
    }

    private static SupplyCheck PerDwarf(string topic, int units, int dwarves, int ok, int low, string why)
    {
        var per = (double)units / dwarves;
        var text = $"{units} ({per:0.#} por anão)";
        return per >= ok ? new(SupplyStatus.Ok, topic, text + ".")
            : new(per >= low ? SupplyStatus.Warning : SupplyStatus.Missing, topic, $"{text}: pouco, {why}.");
    }

    private static IEnumerable<SupplyCheck> CheckPets(EmbarkSnapshot s)
    {
        var chosen = s.Pets.Where(p => p.Count > 0).ToList();
        if (s.Pets.Count == 0) yield break;

        if (!chosen.Any(p => p.HuntsVermin))
        {
            var cat = s.Pets.FirstOrDefault(p => p.HuntsVermin);
            if (cat is not null)
                yield return new(SupplyStatus.Tip, "Caçador de vermes",
                    $"Nenhum {cat.Name}: ratos e outros vermes estragam a comida guardada. Um casal resolve (e procria).");
        }

        foreach (var species in chosen.GroupBy(p => p.Creature))
        {
            var first = species.First();
            var name = first.Name;
            var females = species.Where(p => p.IsFemale).Sum(p => p.Count);
            var males = species.Where(p => p.IsMale).Sum(p => p.Count);
            var sexed = species.Any(p => p.IsFemale || p.IsMale);
            var traits = SpeciesTraits(s, species.Key);
            var useful = traits.Grazer || traits.LaysEggs || traits.Milkable;
            if (sexed && useful && (females == 0 || males == 0))
                yield return new(SupplyStatus.Warning, "Casal", $"{name}: {females} fêmeas e {males} machos, não vai procriar.");
            if (traits.Grazer)
                yield return new(SupplyStatus.Tip, "Pasto", $"{name} pasta: precisa de uma área de pasto com grama, ou morre de fome.");
        }

        if (chosen.Count == 0)
            yield return new(SupplyStatus.Tip, "Animais",
                "Nenhum animal escolhido. Galinhas (ovos), um casal de gatos ou cães e um animal de leite/lã são escolhas comuns.");
    }

    /// <summary>Nome em pt-BR do grupo de itens (o nome do item fica em inglês, como no jogo).</summary>
    public static string Group(string type) => type switch
    {
        "WEAPON" => "Armas e ferramentas",
        "ANVIL" or "TOOL" or "BUCKET" or "BARREL" or "BAG" or "BACKPACK" or "QUIVER" or "FLASK" or "CAGE" or "CHAIN" or "GOBLET" => "Utensílios",
        "SPLINT" or "CRUTCH" or "THREAD" or "CLOTH" => "Médico e tecidos",
        "DRINK" => "Bebida",
        "MEAT" or "FISH" or "PLANT" or "CHEESE" or "PLANT_GROWTH" or "EGG" or "FOOD" => "Comida",
        "SEEDS" => "Sementes",
        "ARMOR" or "HELM" or "GLOVES" or "SHOES" or "PANTS" or "SHIELD" or "AMMO" => "Armadura e munição",
        "WOOD" or "BAR" or "BLOCKS" or "BOULDER" or "ROUGH" or "SMALLGEM" or "GEM" or "SKIN_TANNED" or "SHEET" => "Materiais",
        _ => "Outros",
    };

    private static string PetKind(EmbarkPet p) => p.Profession switch
    {
        "CHILD" => "filhote",
        "TRAINED_HUNTER" => "treinado p/ caça",
        "TRAINED_WAR" => "treinado p/ guerra",
        _ => "adulto",
    };

    private static string PetSex(EmbarkPet p) => p.Caste switch { "FEMALE" => "F", "MALE" => "M", _ => "-" };

    /// <summary>Flags da espécie juntando todas as castas (só a fêmea põe ovos ou dá leite).</summary>
    private static EmbarkPet SpeciesTraits(EmbarkSnapshot s, string creature)
    {
        var all = s.Pets.Where(p => p.Creature == creature).ToList();
        return all[0] with
        {
            Grazer = all.Any(p => p.Grazer),
            LaysEggs = all.Any(p => p.LaysEggs),
            Milkable = all.Any(p => p.Milkable),
            HuntsVermin = all.Any(p => p.HuntsVermin),
        };
    }

    private static string PetTraits(EmbarkPet p) => string.Join(", ", new[]
    {
        p.Grazer ? "pasta" : null,
        p.LaysEggs ? "ovos" : null,
        p.Milkable ? "leite" : null,
        p.HuntsVermin ? "caça vermes" : null,
    }.Where(t => t is not null));

    /// <summary>Seção de itens e animais (subaba e resumo da IA).</summary>
    public static MarkdownBuilder AppendSection(MarkdownBuilder md, EmbarkAnalysis analysis, EmbarkPlan? plan, int level = 2)
    {
        var s = analysis.Snapshot;
        md.Heading(level, "Itens e animais")
          .Paragraph($"Pontos de embarque: **{s.PointsRemaining}** restantes de {s.EmbarkPoints}. " +
                     "Itens e animais são comprados na tela do jogo (o DwarfVolon só lê).");

        md.Heading(level + 1, "Conferência")
          .Paragraph("Referências aproximadas para o primeiro ano, ligadas às funções decididas.")
          .Bullets(Check(analysis, plan).Select(c => $"{c.Icon} **{c.Topic}**: {c.Text}"));

        md.Heading(level + 1, "Itens escolhidos");
        if (s.Items.Count == 0)
            md.Paragraph("Nenhum item.");
        else
            md.Table(["Grupo", "Item", "Qtd"],
                s.Items.OrderBy(i => Group(i.Type)).ThenBy(i => i.Name)
                    .Select(i => (IReadOnlyList<object?>)[Group(i.Type), i.Name, i.Stack]),
                [MdAlign.Left, MdAlign.Left, MdAlign.Right]);

        md.Heading(level + 1, "Animais escolhidos");
        var chosen = s.Pets.Where(p => p.Count > 0).ToList();
        if (chosen.Count == 0)
            md.Paragraph("Nenhum.");
        else
            md.Table(["Animal", "Sexo", "Tipo", "Qtd", "Valor", "Uso"],
                chosen.Select(p => (IReadOnlyList<object?>)[p.Name, PetSex(p), PetKind(p), p.Count, p.Value, PetTraits(SpeciesTraits(s, p.Creature))]),
                [MdAlign.Left, MdAlign.Center, MdAlign.Left, MdAlign.Right, MdAlign.Right, MdAlign.Left]);

        if (s.Pets.Count > 0)
            md.Heading(level + 1, "Animais disponíveis")
              .Paragraph("Valor = valor do animal no jogo (quanto maior, mais caro no embarque).")
              .Table(["Animal", "Valor", "Uso", "Opções"],
                  s.Pets.GroupBy(p => p.Creature).Select(g => (IReadOnlyList<object?>)
                  [
                      g.First().Name,
                      g.Max(p => p.Value),
                      PetTraits(SpeciesTraits(s, g.Key)),
                      string.Join(", ", g.Select(p => p.Profession).Distinct().Select(pr => PetKind(g.First(p => p.Profession == pr)))),
                  ]),
                  [MdAlign.Left, MdAlign.Right, MdAlign.Left, MdAlign.Left]);

        return md;
    }

    /// <summary>Documento da subaba "Itens e animais".</summary>
    public static string Build(EmbarkAnalysis analysis, EmbarkPlan? plan = null) =>
        AppendSection(new MarkdownBuilder(), analysis, plan, level: 1).ToString();
}
