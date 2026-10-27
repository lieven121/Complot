using System.Globalization;
using System.Text.RegularExpressions;

namespace Complot.Domain.Catalog;

/// <summary>
/// Stock and offers expressed in pieces (seeds, plants, bulbs…), to compare with a number of plants. A weight anywhere
/// in the unit ("g", "zakje 4 g", "1 kg pootgoed") becomes pieces through the variety's seeds per gram; otherwise
/// the unit ends in a count, optionally with an amount ("zaden", "zakje 25 z.", "per plant").
/// </summary>
public static partial class Pieces
{
    private static readonly HashSet<string> CountUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "st", "st.", "stuk", "stuks", "x", "pcs", "piece", "pieces",
        "z", "z.", "zaad", "zaden", "zaadjes", "seed", "seeds",
        "plant", "planten", "plantjes", "plants", "plug", "plugs", "plugplanten",
        "struik", "struiken", "shrub", "shrubs", "boom", "bomen", "tree", "trees",
        "bol", "bollen", "bulb", "bulbs", "knol", "knollen", "tuber", "tubers", "pootgoed",
        "uitloper", "uitlopers", "runner", "runners", "stek", "stekken", "cutting", "cuttings",
    };

    private static readonly Dictionary<string, decimal> Grams = new(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = 1, ["gr"] = 1, ["gram"] = 1, ["kg"] = 1000,
    };

    [GeneratedRegex(@"(?:^|\s)(?<amount>\d+(?:[.,]\d+)?)?\s*(?<unit>kg|gr|gram|g)(?=\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex WeightPattern();

    [GeneratedRegex(@"^(?<label>.*?)\s*(?<amount>\d+(?:[.,]\d+)?)?\s*(?<unit>[\p{L}.]+)$")]
    private static partial Regex CountPattern();

    /// <summary>Pieces in <paramref name="quantity"/> × <paramref name="unit"/>; null when the unit is no count or weight (or the weight has no seeds per gram).</summary>
    public static decimal? Of(decimal quantity, string? unit, decimal? seedsPerGram)
    {
        if (string.IsNullOrWhiteSpace(unit)) return null;
        var text = unit.Trim();
        if (WeightPattern().Match(text) is { Success: true } weight)
            return seedsPerGram is > 0 and var perGram ? quantity * Amount(weight) * Grams[weight.Groups["unit"].Value] * perGram : null;
        return CountPattern().Match(text) is { Success: true } count && CountUnits.Contains(count.Groups["unit"].Value)
            ? quantity * Amount(count)
            : null;
    }

    private static decimal Amount(Match m) =>
        m.Groups["amount"].Success ? decimal.Parse(m.Groups["amount"].Value.Replace(',', '.'), CultureInfo.InvariantCulture) : 1;
}
