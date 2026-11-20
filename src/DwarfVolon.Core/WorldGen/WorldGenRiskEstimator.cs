using System.Globalization;

namespace DwarfVolon.Core.WorldGen;

public enum RiskLevel
{
    Low,
    Medium,
    High,
}

public sealed record RiskItem(string Label, RiskLevel Level, string Detail);

/// <summary>
/// Estima o risco de o jogo rejeitar mapas com um conjunto de parâmetros, sem gerar o mundo.
///
/// Modelo (aproximação): cada campo (elevação, chuva, drenagem, selvageria, vulcanismo) é sorteado
/// entre o mínimo e o máximo do token de faixa, com a chance de cada quinto da faixa dada pelos
/// pesos do token *_FREQUENCY, e uniforme dentro de cada quinto. A partir disso calcula-se quantos
/// quadrados cairiam em cada faixa baixa/média/alta e compara-se com os mínimos exigidos
/// (*_RANGES, REGION_COUNTS de montanha e oceano).
///
/// As faixas de elevação vêm das mensagens do próprio jogo (0–99, 100–299, 300–400); as demais
/// usam terços (0–33, 34–66, 67–100), coerente com "High savagery (66+)". O resultado real tem
/// ruído, por isso a margem exigida para "baixo" é de 25%.
/// </summary>
public static class WorldGenRiskEstimator
{
    /// <summary>Folga mínima (esperado / exigido) para considerar o risco baixo.</summary>
    public const double ComfortableRatio = 1.25;

    private static readonly (double From, double To)[] Thirds = [(0, 34), (34, 67), (67, 101)];
    private static readonly (double From, double To)[] ElevationBands = [(0, 100), (100, 300), (300, 401)];
    private static readonly string[] BandNames = ["baixo", "médio", "alto"];

    private sealed record Field(string Label, string Range, string Frequency, string Minimums, (double From, double To)[] Bands);

    private static readonly Field[] Fields =
    [
        new("Elevação", "ELEVATION", "ELEVATION_FREQUENCY", "ELEVATION_RANGES", ElevationBands),
        new("Chuva", "RAINFALL", "RAIN_FREQUENCY", "RAIN_RANGES", Thirds),
        new("Drenagem", "DRAINAGE", "DRAINAGE_FREQUENCY", "DRAINAGE_RANGES", Thirds),
        new("Selvageria", "SAVAGERY", "SAVAGERY_FREQUENCY", "SAVAGERY_RANGES", Thirds),
        new("Vulcanismo", "VOLCANISM", "VOLCANISM_FREQUENCY", "VOLCANISM_RANGES", Thirds),
    ];

    /// <summary>Parâmetros de referência: padrão do jogo para região média (VOLCANO_MIN 3 com pesos iguais).</summary>
    private const double ReferenceVolcanoes = 3;
    private const double ReferenceArea = 129 * 129;
    private const double ReferenceTopShare = 0.2;

    public static IReadOnlyList<RiskItem> Estimate(WorldGenParameterSet set)
    {
        var items = new List<RiskItem>();
        if (set.Dimensions is not { } dim) return items;
        double area = dim.Width * dim.Height;

        foreach (var field in Fields)
        {
            var shares = Shares(set, field);
            if (shares is null) continue;
            var minimums = Numbers(set.Find(field.Minimums));
            for (var band = 0; band < 3 && band < minimums.Count; band++)
                items.Add(Compare($"{field.Label} {BandNames[band]}", shares[band] * area, minimums[band]));
        }

        var elevation = Shares(set, Fields[0]);
        if (elevation is not null)
        {
            if (Numbers(set.Find("REGION_COUNTS", "MOUNTAINS")) is { Count: > 0 } mountains)
                items.Add(Compare("Área de montanha", elevation[2] * area, mountains[0]));
            if (Numbers(set.Find("REGION_COUNTS", "OCEAN")) is { Count: > 0 } ocean)
                items.Add(Compare("Área de oceano", elevation[0] * area, ocean[0]));
        }

        var savagery = Shares(set, Fields[3]);
        if (savagery is not null)
        {
            var calm = savagery[0] + savagery[1];
            items.Add(new RiskItem("Espaço para civilizações",
                calm >= 0.5 ? RiskLevel.Low : calm >= 0.3 ? RiskLevel.Medium : RiskLevel.High,
                $"{Percent(calm)} do mapa com selvageria abaixo de 66 (acima disso não há civilizações)."));
        }

        items.Add(Volcanoes(set, area));
        return items;
    }

    /// <summary>O pior nível entre os itens.</summary>
    public static RiskLevel Overall(IEnumerable<RiskItem> items) => items.Select(i => i.Level).DefaultIfEmpty(RiskLevel.Low).Max();

    public static string Describe(RiskLevel level) => level switch
    {
        RiskLevel.Low => "baixo",
        RiskLevel.Medium => "médio",
        _ => "alto",
    };

    private static RiskItem Volcanoes(WorldGenParameterSet set, double area)
    {
        var required = Numbers(set.Find("VOLCANO_MIN")).FirstOrDefault();
        if (required <= 0) return new RiskItem("Vulcões", RiskLevel.Low, "Nenhum vulcão exigido.");

        var range = Numbers(set.Find("VOLCANISM"));
        if (range.Count >= 2 && range[1] < 100)
            return new RiskItem("Vulcões", RiskLevel.High, $"Exige {required} vulcões, mas o vulcanismo máximo é {range[1]}: vulcões só nascem com vulcanismo 100.");

        var weights = Numbers(set.Find("VOLCANISM_FREQUENCY")).Skip(1).ToList();
        var topShare = weights.Count == 5 && weights.Sum() > 0 ? weights[4] / weights.Sum() : ReferenceTopShare;
        var reference = ReferenceVolcanoes * (area / ReferenceArea) * (topShare / ReferenceTopShare);
        var ratio = required / reference;
        var level = ratio <= 2 ? RiskLevel.Low : ratio <= 4 ? RiskLevel.Medium : RiskLevel.High;
        return new RiskItem("Vulcões", level,
            $"Exige {required}; pela distribuição, o equivalente ao padrão do jogo seria ~{reference:0.#} ({ratio:0.#}x). " +
            "A quantidade real de pontos com vulcanismo 100 só se confirma gerando o mundo.");
    }

    private static RiskItem Compare(string label, double expected, double required)
    {
        if (required <= 0) return new RiskItem(label, RiskLevel.Low, "Sem mínimo exigido.");
        var ratio = expected / required;
        var level = ratio >= ComfortableRatio ? RiskLevel.Low : ratio >= 1 ? RiskLevel.Medium : RiskLevel.High;
        return new RiskItem(label, level, $"Esperado ~{expected:0} quadrados, exigido {required:0} ({ratio:0.00}x).");
    }

    /// <summary>Fração esperada do mapa em cada faixa (baixa, média, alta).</summary>
    private static double[]? Shares(WorldGenParameterSet set, Field field)
    {
        var range = Numbers(set.Find(field.Range));
        if (range.Count < 2) return null;
        double min = range[0], max = range[1];

        var weights = Numbers(set.Find(field.Frequency)).Skip(1).ToList();
        if (weights.Count != 5 || weights.Sum() <= 0) weights = [1, 1, 1, 1, 1];
        var total = weights.Sum();

        var shares = new double[3];
        if (max <= min)
        {
            for (var b = 0; b < 3; b++) shares[b] = min >= field.Bands[b].From && min < field.Bands[b].To ? 1 : 0;
            return shares;
        }

        // Valores inteiros de min a max: o intervalo contínuo é [min, max + 1).
        var width = (max + 1 - min) / 5;
        for (var fifth = 0; fifth < 5; fifth++)
        {
            var from = min + fifth * width;
            var to = from + width;
            for (var b = 0; b < 3; b++)
            {
                var overlap = Math.Max(0, Math.Min(to, field.Bands[b].To) - Math.Max(from, field.Bands[b].From));
                shares[b] += overlap / width * weights[fifth] / total;
            }
        }
        return shares;
    }

    private static List<double> Numbers(WorldGenToken? token) =>
        token?.Values.Select(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0).ToList() ?? [];

    private static string Percent(double share) => share.ToString("P0", CultureInfo.GetCultureInfo("pt-BR"));
}
