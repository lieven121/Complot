using Complot.Domain.Catalog;
using Complot.Domain.Garden;

namespace Complot.Domain.Plantings;

/// <summary>
/// A tree or planting that takes part in pollination, at a point on the site map (m) during [From, To].
/// <see cref="Plants"/> is null when unknown (sown broadcast: many). <see cref="CloneOf"/> is set for grafted trees:
/// two trees of the same cultivar are genetically the same and do not pollinate each other.
/// </summary>
public sealed record Flowering(
    Guid Id, string Name, Pollination Pollination, double X, double Y, DateOnly From, DateOnly To, int? Plants, string? CloneOf)
{
    public static Flowering? Of(Tree tree) => tree.Pollination is not { } p ? null
        : new(tree.Id, tree.Name, p, tree.X, tree.Y, tree.PlantedOn ?? DateOnly.MinValue, DateOnly.MaxValue, 1,
            tree.Cultivar?.Trim().ToLowerInvariant());

    /// <summary>Needs Bed and Variety loaded; a spot sits at its point, a strip at the middle of the bed.</summary>
    public static Flowering? Of(Planting planting)
    {
        if (planting.Variety.Pollination is not { } p) return null;
        var bed = planting.Bed.Shape;
        var (x, y) = planting is { SpotX: { } sx, SpotY: { } sy } ? (bed.X + sx, bed.Y + sy) : (bed.X + bed.Width / 2, bed.Y + bed.Height / 2);
        return new(planting.Id, $"{planting.Variety.Name} ({planting.Bed.Code})", p, x, y, planting.StartInBed, planting.EndInBed,
            planting.PlantCount(), null);
    }

    public double DistanceTo(Flowering other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
}

public sealed record Pollinator(Flowering Partner, double Distance);

public static class PollinationCheck
{
    public static List<Flowering> Collect(IEnumerable<Tree> trees, IEnumerable<Planting> plantings) =>
        trees.Select(Flowering.Of).Concat(plantings.Select(Flowering.Of)).OfType<Flowering>().ToList();

    /// <summary>
    /// The plants within range that can pollinate <paramref name="subject"/> while it is there, nearest first; null
    /// when it needs none (self-fertile, male, or a planting of several plants that pollinate each other).
    /// </summary>
    public static List<Pollinator>? PollinatorsOf(Flowering subject, IEnumerable<Flowering> all)
    {
        var p = subject.Pollination;
        if (!p.NeedsPollinator || (p.Need == PollinationNeed.Partner && subject.Plants is null or > 1)) return null;
        return all
            .Where(o => o.Id != subject.Id && p.IsPollinatedBy(o.Pollination) && o.From <= subject.To && subject.From <= o.To
                        && (subject.CloneOf is null || subject.CloneOf != o.CloneOf))
            .Select(o => new Pollinator(o, subject.DistanceTo(o)))
            .Where(x => x.Distance <= p.RangeMeters)
            .OrderBy(x => x.Distance)
            .ToList();
    }
}
