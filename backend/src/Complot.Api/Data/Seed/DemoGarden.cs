using System.Text.Json;
using System.Text.Json.Serialization;
using Complot.Domain.Catalog;
using Complot.Domain.Common;
using Complot.Domain.Garden;
using Complot.Domain.Plantings;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Data.Seed;

/// <summary>Sample sites, layout and plantings from the clickable demo (demo-garden.json, embedded).</summary>
internal static class DemoGarden
{
    private sealed record SeedShape(ShapeKind Kind, double X, double Y, double Width, double Height)
    {
        public Shape ToDomain() => new(Kind, X, Y, Width, Height);
    }

    private sealed record SeedPlot(string Key, string Name, PlotType Type, SeedShape Shape);
    private sealed record SeedBed(string Code, string Plot, SeedShape Shape, SunExposure Sun, string? Soil);
    private sealed record SeedPath(string Name, PathMaterial Material, SeedShape Shape);
    private sealed record SeedFeature(string Name, FeatureType Type, SeedShape Shape);
    private sealed record SeedTree(string Name, string? Cultivar, TreeKind Kind, double X, double Y, double CrownDiameter, DateOnly? PlantedOn, List<YearlyAction> Actions);
    private sealed record SeedSite(string Name, List<SeedPlot> Plots, List<SeedBed> Beds, List<SeedPath> Paths, List<SeedFeature> Features, List<SeedTree> Trees);
    private sealed record SeedPlanting(string Bed, string Variety, string? Cultivar, double Share, DateOnly SowOn, DateOnly? PlantOutOn, DateOnly? HarvestFrom, DateOnly? HarvestTo, DateOnly? ClearOn);
    private sealed record SeedFile(List<SeedSite> Sites, List<SeedPlanting> Plantings);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static async Task AddToAsync(ComplotDbContext db, CancellationToken ct)
    {
        await using var stream = typeof(DemoGarden).Assembly.GetManifestResourceStream("Complot.Api.Data.Seed.demo-garden.json")
            ?? throw new InvalidOperationException("Embedded demo-garden.json not found");
        var seed = await JsonSerializer.DeserializeAsync<SeedFile>(stream, Json, ct) ?? throw new InvalidOperationException("demo-garden.json is empty");

        var bedsByCode = new Dictionary<string, Bed>();
        foreach (var s in seed.Sites)
        {
            var site = new Site { Name = s.Name };
            db.Sites.Add(site);
            var plots = s.Plots.ToDictionary(p => p.Key, p => new Plot { SiteId = site.Id, Name = p.Name, Type = p.Type, Shape = p.Shape.ToDomain() });
            db.Plots.AddRange(plots.Values);
            foreach (var b in s.Beds)
            {
                var bed = new Bed { SiteId = site.Id, PlotId = plots[b.Plot].Id, Code = b.Code, Shape = b.Shape.ToDomain(), Sun = b.Sun, Soil = b.Soil };
                bedsByCode[b.Code] = bed;
                db.Beds.Add(bed);
            }
            db.Paths.AddRange(s.Paths.Select(p => new GardenPath { SiteId = site.Id, Name = p.Name, Material = p.Material, Shape = p.Shape.ToDomain() }));
            db.Features.AddRange(s.Features.Select(f => new GardenFeature { SiteId = site.Id, Name = f.Name, Type = f.Type, Shape = f.Shape.ToDomain() }));
            db.Trees.AddRange(s.Trees.Select(t => new Tree
            {
                SiteId = site.Id, Name = t.Name, Cultivar = t.Cultivar, Kind = t.Kind, X = t.X, Y = t.Y,
                CrownDiameter = t.CrownDiameter, PlantedOn = t.PlantedOn, Actions = t.Actions,
            }));
        }

        var varieties = await db.Varieties.ToDictionaryAsync(v => (v.Name, v.Cultivar), ct);
        foreach (var p in seed.Plantings)
        {
            var planting = new Planting { BedId = bedsByCode[p.Bed].Id, VarietyId = varieties[(p.Variety, p.Cultivar)].Id, Share = p.Share };
            planting.ApplyDates(new PlantingDates(p.SowOn, p.PlantOutOn, p.HarvestFrom, p.HarvestTo, p.ClearOn));
            db.Plantings.Add(planting);
        }
        await db.SaveChangesAsync(ct);
    }
}
