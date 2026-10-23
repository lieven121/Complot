using Complot.Api.Common;
using Complot.Domain.Catalog;

namespace Complot.Api.Features.Catalog.Varieties;

public static class VarietyMapping
{
    public static VarietyDto ToDto(this PlantVariety v, IEnumerable<InventoryLot> lots, DateOnly today) => new(
        v.Id, v.CategoryId, v.FamilyId, v.EffectiveFamily?.Id, v.RotationRestYears,
        v.Name, v.Cultivar, v.LatinName, v.Life,
        v.SowDepthCm, v.SpacingInRowCm, v.SpacingBetweenRowsCm, v.HeightCm, v.Sun,
        v.SowIndoor.ToDto(), v.WeeksIndoor, v.SowOutdoor.ToDto(), v.PlantOut.ToDto(), v.Harvest.ToDto()!,
        v.Germination.ToDto(), v.DaysToFirstHarvest, v.HarvestWeeks, v.MaxYearsInBed, v.Notes,
        v.Actions.Select(a => new VarietyActionDto(a.Title, a.Month, a.EstimatedMinutes)).ToArray(),
        v.Suppliers.OrderBy(s => s.Price)
            .Select(s => new VarietySupplierDto(s.SupplierId, s.Supplier.Name, s.Supplier.IsOwnSource, s.Price, s.Unit, s.Url)).ToArray(),
        lots.Select(l => new StockDto(l.Id, l.Quantity, l.Unit, l.StatusOn(today))).ToArray());

    public static void Apply(this VarietyInput input, PlantVariety v)
    {
        v.CategoryId = input.CategoryId;
        v.FamilyId = input.FamilyId;
        v.Name = input.Name.Trim();
        v.Cultivar = Clean(input.Cultivar);
        v.LatinName = Clean(input.LatinName);
        v.Life = input.Life;
        v.SowDepthCm = input.SowDepthCm;
        v.SpacingInRowCm = input.SpacingInRowCm;
        v.SpacingBetweenRowsCm = input.SpacingBetweenRowsCm;
        v.HeightCm = input.HeightCm;
        v.Sun = input.Sun.Distinct().Order().ToList();
        v.SowIndoor = input.SowIndoor.ToDomain();
        v.WeeksIndoor = input.SowIndoor is null ? null : input.WeeksIndoor;
        v.SowOutdoor = input.SowOutdoor.ToDomain();
        v.PlantOut = input.PlantOut.ToDomain();
        v.Harvest = input.Harvest.ToDomain()!;
        v.Germination = input.Germination.ToDomain();
        v.DaysToFirstHarvest = input.DaysToFirstHarvest;
        v.HarvestWeeks = input.HarvestWeeks;
        v.MaxYearsInBed = input.MaxYearsInBed;
        v.Notes = Clean(input.Notes);
        v.Actions = input.Actions.Select(a => new VarietyAction { Title = a.Title.Trim(), Month = a.Month, EstimatedMinutes = a.EstimatedMinutes }).ToList();
        v.Suppliers.SyncWith(input.Suppliers, s => s.SupplierId, s => s.SupplierId,
            s => new VarietySupplier { VarietyId = v.Id, SupplierId = s.SupplierId, Price = s.Price, Unit = Clean(s.Unit), Url = Clean(s.Url) },
            (s, existing) => { existing.Price = s.Price; existing.Unit = Clean(s.Unit); existing.Url = Clean(s.Url); });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
