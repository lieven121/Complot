namespace Complot.Domain.Catalog;

/// <summary>An inclusive range of days, e.g. germination takes 7–14 days.</summary>
public sealed record DayRange
{
    public DayRange(int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(min);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, min);
        Min = min;
        Max = max;
    }

    public int Min { get; }
    public int Max { get; }
}
