using Complot.Domain.Costs;

namespace Complot.Domain.Tests.Costs;

public sealed class CostReportTests
{
    private static readonly Guid Seeds = Guid.NewGuid();
    private static readonly Guid Soil = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static Invoice Invoice(string date, params (Guid Category, decimal Amount)[] lines) => new()
    {
        Date = DateOnly.Parse(date), Supplier = "x", Total = lines.Sum(l => l.Amount),
        Lines = lines.Select(l => new InvoiceLine { Description = "-", CategoryId = l.Category, Amount = l.Amount }).ToList(),
    };

    private static readonly Invoice[] Invoices =
    [
        Invoice("2025-01-18", (Seeds, 32.40m), (Soil, 6m)),
        Invoice("2025-09-20", (Soil, 27.50m)),
        Invoice("2026-02-02", (Seeds, 19.60m)),
        Invoice("2026-05-02", (Soil, 30m)),
        Invoice("2026-10-05", (Soil, 32m)),
    ];

    [Fact]
    public void One_year_by_quarter()
    {
        var report = CostReport.Build(Invoices, 2026, CostGrouping.Quarter, Today);
        Assert.Equal((81.60m, 3), (report.Total, report.InvoiceCount));
        Assert.Equal([19.60m, 30m, 0m, 32m], report.Periods.Select(p => p.Total));
        Assert.Equal(19.60m, report.Periods[0].ByCategory[Seeds]);
        Assert.Equal(62m, report.ByCategory[Soil]);
        Assert.Equal(6, report.Months); // this year: January–June so far
        Assert.Equal(13.60m, report.AveragePerMonth);
    }

    [Fact]
    public void Year_to_date_compares_with_the_same_day_last_year()
    {
        var report = CostReport.Build(Invoices, null, CostGrouping.Year, Today);
        Assert.Equal(49.60m, report.YearToDate);            // Feb + May 2026
        Assert.Equal(38.40m, report.YearToDatePreviousYear); // January 2025 only
    }

    [Fact]
    public void All_years_by_year_and_category_per_year()
    {
        var report = CostReport.Build(Invoices, null, CostGrouping.Year, Today);
        Assert.Equal([2025, 2026], report.Years);
        Assert.Equal([(2025, 65.90m), (2026, 81.60m)], report.Periods.Select(p => (p.Year, p.Total)));
        Assert.Equal(33.50m, report.ByCategoryAndYear[Soil][2025]);
        Assert.Equal(18, report.Months); // January 2025 – June 2026
    }

    [Fact]
    public void Months_per_month_and_empty_data()
    {
        Assert.Equal(12, CostReport.Build(Invoices, 2025, CostGrouping.Month, Today).Periods.Count);
        var empty = CostReport.Build([], null, CostGrouping.Month, Today);
        Assert.Equal((0m, 0, 0m), (empty.Total, empty.Months, empty.AveragePerMonth));
        Assert.Empty(empty.Periods);
    }
}
