using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Api.Features.Catalog.Categories;
using Complot.Api.Features.Catalog.Varieties;
using Complot.Domain.Catalog;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Catalog.Exchange;

/// <summary>
/// Imports the exchange format. Rows are translated into the regular inputs and checked by the same validators
/// as the CRUD endpoints. A preview runs the full import inside a transaction that is rolled back, so preview and
/// import can never disagree.
/// </summary>
public sealed class CatalogImporter(
    ComplotDbContext db,
    IValidator<CategoryInput> categoryValidator,
    IValidator<VarietyInput> varietyValidator)
{
    private Dictionary<string, PlantFamily> _families = [];
    private Dictionary<string, Supplier> _suppliers = [];
    private Dictionary<string, PlantCategory> _categories = [];

    public async Task<ImportReport> RunAsync(CatalogExchange data, bool commit, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        _families = await db.Families.ToDictionaryAsync(f => f.LatinName, StringComparer.OrdinalIgnoreCase, ct);
        _suppliers = await db.Suppliers.ToDictionaryAsync(s => s.Name, StringComparer.OrdinalIgnoreCase, ct);
        _categories = await db.Categories.ToDictionaryAsync(c => c.Slug, StringComparer.OrdinalIgnoreCase, ct);

        var categoryRows = await ImportCategoriesAsync(data.Categories ?? [], ct);
        var varietyRows = await ImportVarietiesAsync(data.Varieties ?? [], ct);

        if (commit) await transaction.CommitAsync(ct);
        else
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
        }
        return new ImportReport(commit, categoryRows, varietyRows);
    }

    private async Task<List<ImportRow>> ImportCategoriesAsync(List<ExchangeCategory> rows, CancellationToken ct)
    {
        var report = new List<ImportRow>();
        var imported = new List<(ExchangeCategory Row, PlantCategory Category, List<ErrorDetail> Warnings, int Index)>();

        // Pass 1: the categories themselves (neighbours may refer to categories later in the file).
        foreach (var (row, index) in rows.Select((r, i) => (r, i)))
        {
            var warnings = new List<ErrorDetail>();
            var slug = row.Id?.Trim().ToLowerInvariant() ?? "";
            var existing = _categories.GetValueOrDefault(slug);
            var input = new CategoryInput(
                slug,
                row.Name ?? existing?.Name ?? "",
                row.Icon ?? existing?.Icon ?? "🌱",
                row.Color ?? existing?.Color ?? "#3f7d4e",
                ResolveFamily(row.Family, warnings) ?? existing?.DefaultFamilyId,
                row.RotationExempt ?? existing?.RotationExempt ?? false,
                existing?.CompanionIds(CompanionRelation.Good).ToList() ?? [],
                existing?.CompanionIds(CompanionRelation.Bad).ToList() ?? []);

            await db.SaveChangesAsync(ct); // a newly created family must exist before validation
            var result = await categoryValidator.ValidateAsync(input, ct);
            if (!result.IsValid)
            {
                report.Add(new ImportRow(index, row.Name ?? slug, slug, ImportRowStatus.Error, FieldErrors.From(result), warnings));
                continue;
            }
            var category = existing ?? new PlantCategory { Slug = slug, Name = "" };
            input.Apply(category);
            if (existing is null)
            {
                db.Categories.Add(category);
                _categories[slug] = category;
            }
            await db.SaveChangesAsync(ct);
            imported.Add((row, category, warnings, index));
            report.Add(new ImportRow(index, category.Name, slug, existing is null ? ImportRowStatus.New : ImportRowStatus.Update, null, warnings));
        }

        // Pass 2: companion rules, now that every category in the file exists.
        foreach (var (row, category, warnings, _) in imported.Where(x => x.Row.GoodNeighbours is not null || x.Row.BadNeighbours is not null))
        {
            var input = new CategoryInput(category.Slug, category.Name, category.Icon, category.Color, category.DefaultFamilyId, category.RotationExempt,
                ResolveCategories(row.GoodNeighbours, warnings) ?? category.CompanionIds(CompanionRelation.Good).ToList(),
                ResolveCategories(row.BadNeighbours, warnings) ?? category.CompanionIds(CompanionRelation.Bad).ToList());
            input.Apply(category);
        }
        await db.SaveChangesAsync(ct);
        return report;
    }

    private async Task<List<ImportRow>> ImportVarietiesAsync(List<ExchangeVariety> rows, CancellationToken ct)
    {
        var report = new List<ImportRow>();
        var existingVarieties = (await db.Varieties.Include(v => v.Suppliers).ToListAsync(ct))
            .ToDictionary(v => VarietyKey(v.Name, v.Cultivar));

        foreach (var (row, index) in rows.Select((r, i) => (r, i)))
        {
            var errors = new Dictionary<string, List<ErrorDetail>>();
            var warnings = new List<ErrorDetail>();
            var input = ToInput(row, errors, warnings);

            await db.SaveChangesAsync(ct); // newly created families/suppliers must exist before validation
            var result = await varietyValidator.ValidateAsync(input, ct);
            foreach (var (field, details) in FieldErrors.From(result))
            {
                if (field == "categoryId" && errors.ContainsKey("category")) continue; // already reported by slug
                AddErrors(errors, field, details);
            }

            if (errors.Count > 0)
            {
                report.Add(new ImportRow(index, row.Name ?? "?", row.Cultivar, ImportRowStatus.Error,
                    errors.ToDictionary(e => e.Key, e => e.Value.ToArray()), warnings));
                continue;
            }

            var key = VarietyKey(input.Name, input.Cultivar);
            var existing = existingVarieties.GetValueOrDefault(key);
            var variety = existing ?? new PlantVariety { Name = "", Harvest = new MonthWindow(1, 1) };
            input.Apply(variety);
            if (existing is null)
            {
                db.Varieties.Add(variety);
                existingVarieties[key] = variety;
            }
            await db.SaveChangesAsync(ct);
            report.Add(new ImportRow(index, variety.Name, variety.Cultivar, existing is null ? ImportRowStatus.New : ImportRowStatus.Update, null, warnings));
        }
        return report;
    }

    private VarietyInput ToInput(ExchangeVariety row, Dictionary<string, List<ErrorDetail>> errors, List<ErrorDetail> warnings)
    {
        var category = row.Category is null ? null : _categories.GetValueOrDefault(row.Category.Trim());
        if (category is null) AddError(errors, "category", new ErrorDetail("unknownCategory", Params(("slug", row.Category))));

        var life = Life.Annual;
        if (row.Life is not null && !Enum.TryParse(row.Life, ignoreCase: true, out life))
            AddError(errors, "life", new ErrorDetail("invalidValue", Params(("value", row.Life))));

        var sun = new List<SunExposure>();
        foreach (var value in row.Sun ?? [])
        {
            if (Enum.TryParse<SunExposure>(value, ignoreCase: true, out var exposure)) sun.Add(exposure);
            else AddError(errors, "sun", new ErrorDetail("invalidValue", Params(("value", value))));
        }
        if (row.Sun is null or []) sun.Add(SunExposure.Full);

        // Only store a family on the variety when it differs from the category default.
        var familyId = ResolveFamily(row.Family, warnings);
        if (familyId == category?.DefaultFamilyId) familyId = null;

        var germination = Pair(row.GerminationDays, "germinationDays", errors);
        return new VarietyInput(
            category?.Id ?? Guid.Empty,
            familyId,
            row.Name ?? "",
            row.Cultivar,
            row.Latin,
            life,
            row.SowDepthCm,
            row.SpacingInRowCm,
            row.SpacingBetweenRowsCm,
            row.HeightCm,
            sun,
            Window(row.SowIndoorMonths, "sowIndoorMonths", errors),
            row.WeeksIndoorBeforePlanting,
            Window(row.SowOutdoorMonths, "sowOutdoorMonths", errors),
            Window(row.PlantOutMonths, "plantOutMonths", errors),
            Window(row.HarvestMonths, "harvestMonths", errors)!,
            germination is null ? null : new DayRangeDto(germination.Value.A, germination.Value.B),
            row.DaysToFirstHarvest,
            row.HarvestWeeks,
            row.MaxYearsInBed ?? (life == Life.Annual ? 1 : null),
            row.Notes,
            (row.Actions ?? []).Select(a => new YearlyActionDto(a.Title, a.Month, a.EstimatedMinutes)).ToList(),
            (row.Suppliers ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Name)).DistinctBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(s => new VarietySupplierInput(ResolveSupplier(s.Name, warnings), s.Price ?? 0, s.Unit, s.Url, s.PiecesPerUnit)).ToList(),
            Pollination: ToPollination(row.Pollination, errors),
            SeedsPerGram: row.SeedsPerGram);
    }

    /// <summary>Null when the row has none; the group and range are checked by the variety validator.</summary>
    private static PollinationDto? ToPollination(ExchangePollination? row, Dictionary<string, List<ErrorDetail>> errors)
    {
        if (row is null) return null;
        var need = PollinationNeed.SelfFertile;
        if (row.Need is not null && !Enum.TryParse(row.Need, ignoreCase: true, out need))
            AddError(errors, "pollination", new ErrorDetail("invalidValue", Params(("value", row.Need))));
        return new PollinationDto(need, row.Group?.Trim() ?? "", row.RangeMeters ?? Pollination.DefaultRangeMeters);
    }

    private Guid? ResolveFamily(string? latinName, List<ErrorDetail> warnings)
    {
        if (string.IsNullOrWhiteSpace(latinName)) return null;
        var name = latinName.Trim();
        if (_families.TryGetValue(name, out var family)) return family.Id;
        family = new PlantFamily { LatinName = name, NameNl = name, NameEn = name, RotationRestYears = 0 };
        db.Families.Add(family);
        _families[name] = family;
        warnings.Add(new ErrorDetail("newFamily", Params(("name", name))));
        return family.Id;
    }

    private Guid ResolveSupplier(string name, List<ErrorDetail> warnings)
    {
        var trimmed = name.Trim();
        if (_suppliers.TryGetValue(trimmed, out var supplier)) return supplier.Id;
        supplier = new Supplier { Name = trimmed };
        db.Suppliers.Add(supplier);
        _suppliers[trimmed] = supplier;
        warnings.Add(new ErrorDetail("newSupplier", Params(("name", trimmed))));
        return supplier.Id;
    }

    private List<Guid>? ResolveCategories(List<string>? slugs, List<ErrorDetail> warnings)
    {
        if (slugs is null) return null;
        var ids = new List<Guid>();
        foreach (var slug in slugs)
        {
            if (_categories.TryGetValue(slug.Trim(), out var category)) ids.Add(category.Id);
            else warnings.Add(new ErrorDetail("unknownNeighbour", Params(("slug", slug))));
        }
        return ids;
    }

    private static MonthWindowDto? Window(int[]? months, string field, Dictionary<string, List<ErrorDetail>> errors) =>
        Pair(months, field, errors) is { } pair ? new MonthWindowDto(pair.A, pair.B) : null;

    private static (int A, int B)? Pair(int[]? values, string field, Dictionary<string, List<ErrorDetail>> errors)
    {
        if (values is null) return null;
        if (values.Length == 2) return (values[0], values[1]);
        AddError(errors, field, new ErrorDetail("pair"));
        return null;
    }

    private static string VarietyKey(string name, string? cultivar) => $"{name.Trim()}|{cultivar?.Trim()}".ToUpperInvariant();

    private static Dictionary<string, object?> Params(params (string Key, object? Value)[] values) => values.ToDictionary(v => v.Key, v => v.Value);

    private static void AddError(Dictionary<string, List<ErrorDetail>> errors, string field, ErrorDetail detail) => AddErrors(errors, field, [detail]);

    private static void AddErrors(Dictionary<string, List<ErrorDetail>> errors, string field, IEnumerable<ErrorDetail> details)
    {
        if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
        list.AddRange(details);
    }
}
