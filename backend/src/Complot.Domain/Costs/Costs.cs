using Complot.Domain.Common;

namespace Complot.Domain.Costs;

public sealed class CostCategory : Entity
{
    public required string Name { get; set; }
    public string Color { get; set; } = "#9a958a";
}

/// <summary>An invoice, split by hand into lines per cost category; the lines add up to the total.</summary>
public sealed class Invoice : Entity
{
    public DateOnly Date { get; set; }
    public required string Supplier { get; set; }
    public string? Number { get; set; }
    public decimal Total { get; set; }

    /// <summary>Optional: the site the costs are for (to compare a home garden with an allotment).</summary>
    public Guid? SiteId { get; set; }

    public string? Notes { get; set; }

    /// <summary>The stored PDF or photo, and its original file name.</summary>
    public string? AttachmentFileName { get; set; }
    public string? AttachmentName { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];
}

public sealed class InvoiceLine : Entity
{
    public Guid InvoiceId { get; set; }
    public required string Description { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
}

public enum CostGrouping
{
    Year,
    Quarter,
    Month,
}

/// <summary>Costs in one period: <see cref="Index"/> is the quarter (1–4) or month (1–12), 0 for a whole year.</summary>
public sealed record CostPeriod(int Year, int Index, IReadOnlyDictionary<Guid, decimal> ByCategory)
{
    public decimal Total => ByCategory.Values.Sum();
}

/// <summary>Everything the cost overview shows, for one year or (<see cref="Year"/> null) all years.</summary>
public sealed record CostReport(
    int? Year,
    decimal Total,
    int InvoiceCount,
    IReadOnlyDictionary<Guid, decimal> ByCategory,
    IReadOnlyList<CostPeriod> Periods,
    decimal YearToDate,
    decimal YearToDatePreviousYear,
    decimal AveragePerMonth,
    int Months,
    IReadOnlyList<int> Years,
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<int, decimal>> ByCategoryAndYear)
{
    public static CostReport Build(IReadOnlyCollection<Invoice> invoices, int? year, CostGrouping grouping, DateOnly today)
    {
        var years = invoices.Select(i => i.Date.Year).Distinct().Order().ToList();
        var shown = invoices.Where(i => year is null || i.Date.Year == year).ToList();
        var lines = shown.SelectMany(i => i.Lines.Select(l => (i.Date, l.CategoryId, l.Amount))).ToList();
        var shownYears = year is { } y ? [y] : years;

        var periods = shownYears.SelectMany(py => PeriodsOf(py, grouping))
            .Select(p => new CostPeriod(p.Year, p.Index, Sum(lines.Where(l => l.Date.Year == p.Year && IndexOf(l.Date, grouping) == p.Index)
                .Select(l => (l.CategoryId, l.Amount)))))
            .ToList();

        var months = MonthsCounted(years, year, today);
        var total = shown.Sum(i => i.Total);
        return new CostReport(
            year, total, shown.Count, Sum(lines.Select(l => (l.CategoryId, l.Amount))), periods,
            SpentUntil(invoices, today.Year, today), SpentUntil(invoices, today.Year - 1, today),
            months == 0 ? 0 : Math.Round(total / months, 2), months, years,
            invoices.SelectMany(i => i.Lines.Select(l => (i.Date.Year, l.CategoryId, l.Amount)))
                .GroupBy(l => l.CategoryId)
                .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<int, decimal>)g.GroupBy(l => l.Year).ToDictionary(y => y.Key, y => y.Sum(l => l.Amount))));
    }

    private static IEnumerable<(int Year, int Index)> PeriodsOf(int year, CostGrouping grouping) => grouping switch
    {
        CostGrouping.Quarter => Enumerable.Range(1, 4).Select(q => (year, q)),
        CostGrouping.Month => Enumerable.Range(1, 12).Select(m => (year, m)),
        _ => [(year, 0)],
    };

    private static int IndexOf(DateOnly date, CostGrouping grouping) => grouping switch
    {
        CostGrouping.Quarter => (date.Month - 1) / 3 + 1,
        CostGrouping.Month => date.Month,
        _ => 0,
    };

    private static Dictionary<Guid, decimal> Sum(IEnumerable<(Guid CategoryId, decimal Amount)> lines) =>
        lines.GroupBy(l => l.CategoryId).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));

    /// <summary>Spent in <paramref name="year"/> up to the same day of the year as <paramref name="today"/>.</summary>
    private static decimal SpentUntil(IEnumerable<Invoice> invoices, int year, DateOnly today) =>
        invoices.Where(i => i.Date.Year == year && (i.Date.Month, i.Date.Day).CompareTo((today.Month, today.Day)) <= 0).Sum(i => i.Total);

    /// <summary>Months to average over: a past year has 12, this year the months so far, all years from the first invoice until now.</summary>
    private static int MonthsCounted(IReadOnlyList<int> years, int? year, DateOnly today) => year switch
    {
        null when years.Count == 0 => 0,
        null => (today.Year - years[0]) * 12 + today.Month,
        { } y when y < today.Year => 12,
        { } y when y == today.Year => today.Month,
        _ => 0,
    };
}
