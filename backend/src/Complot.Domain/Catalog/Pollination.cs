namespace Complot.Domain.Catalog;

/// <summary>
/// What a plant needs to set fruit: <see cref="SelfFertile"/> plants need nothing (but can pollinate others of their
/// group), <see cref="Partner"/> plants need another plant of the group (apple, tomatillo), and
/// <see cref="Female"/> plants need a <see cref="Male"/> one (kiwi, sea buckthorn).
/// </summary>
public enum PollinationNeed
{
    SelfFertile,
    Partner,
    Female,
    Male,
}

/// <summary>
/// Pollination of a variety or tree: plants of the same <see cref="Group"/> (e.g. "Apple", "Kiwi") pollinate each
/// other within <see cref="RangeMeters"/>.
/// </summary>
public sealed record Pollination
{
    public const double DefaultRangeMeters = 20;

    public Pollination(PollinationNeed need, string group, double rangeMeters = DefaultRangeMeters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rangeMeters);
        Need = need;
        Group = group.Trim();
        RangeMeters = rangeMeters;
    }

    public PollinationNeed Need { get; private init; }
    public string Group { get; private init; }
    public double RangeMeters { get; private init; }

    public bool NeedsPollinator => Need is PollinationNeed.Partner or PollinationNeed.Female;

    /// <summary>Can a plant with <paramref name="other"/> pollinate this one (group and sex only)?</summary>
    public bool IsPollinatedBy(Pollination other) =>
        string.Equals(Group, other.Group, StringComparison.OrdinalIgnoreCase)
        && (Need == PollinationNeed.Female ? other.Need == PollinationNeed.Male : other.Need != PollinationNeed.Female);
}
