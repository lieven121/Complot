namespace Complot.Domain.Catalog;

/// <summary>
/// A range of months (1–12), inclusive. A window may wrap around the new year, e.g. October–February.
/// </summary>
public sealed record MonthWindow
{
    public MonthWindow(int from, int to)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(from, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(from, 12);
        ArgumentOutOfRangeException.ThrowIfLessThan(to, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(to, 12);
        From = from;
        To = to;
    }

    public int From { get; private init; }
    public int To { get; private init; }

    public bool WrapsYear => From > To;

    public bool Contains(int month) => WrapsYear ? month >= From || month <= To : month >= From && month <= To;

    public bool Contains(DateOnly date) => Contains(date.Month);
}
