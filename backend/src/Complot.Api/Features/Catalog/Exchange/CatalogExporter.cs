using System.Text.Json;
using Complot.Api.Data;
using Complot.Api.Features.Catalog.Varieties;
using Complot.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Catalog.Exchange;

public static class CatalogExporter
{
    public static async Task<CatalogExchange> ExportAsync(ComplotDbContext db, CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking().Include(c => c.DefaultFamily).OrderBy(c => c.Name).ToListAsync(ct);
        var slugs = categories.ToDictionary(c => c.Id, c => c.Slug);
        var varieties = await db.Varieties.AsNoTracking().WithDetails().OrderBy(v => v.Name).ThenBy(v => v.Cultivar).ToListAsync(ct);

        return new CatalogExchange(
            categories.Select(c => new ExchangeCategory(
                c.Slug, c.Name, c.DefaultFamily?.LatinName, c.Icon, c.Color, c.RotationExempt,
                c.CompanionIds(CompanionRelation.Good).Select(id => slugs[id]).ToList(),
                c.CompanionIds(CompanionRelation.Bad).Select(id => slugs[id]).ToList())).ToList(),
            varieties.Select(v => new ExchangeVariety(
                v.Category.Slug, v.Name, v.Cultivar, v.LatinName, v.EffectiveFamily?.LatinName,
                v.Life.ToString().ToLowerInvariant(),
                v.SowDepthCm, v.SpacingInRowCm, v.SpacingBetweenRowsCm, v.HeightCm,
                v.Sun.Select(s => s.ToString().ToLowerInvariant()).ToList(),
                Months(v.SowIndoor), v.WeeksIndoor, Months(v.SowOutdoor), Months(v.PlantOut),
                v.Germination is null ? null : [v.Germination.Min, v.Germination.Max],
                v.DaysToFirstHarvest, v.HarvestWeeks, Months(v.Harvest), v.MaxYearsInBed, v.Notes,
                v.Actions.Select(a => new ExchangeAction(a.Title, a.Month, a.EstimatedMinutes)).ToList(),
                v.Suppliers.Select(s => new ExchangeSupplier(s.Supplier.Name, s.Price, s.Unit, s.Url)).ToList(),
                v.Pollination is { } p ? new ExchangePollination(JsonNamingPolicy.CamelCase.ConvertName(p.Need.ToString()), p.Group, p.RangeMeters) : null))
                .ToList());
    }

    private static int[]? Months(MonthWindow? window) => window is null ? null : [window.From, window.To];
}
