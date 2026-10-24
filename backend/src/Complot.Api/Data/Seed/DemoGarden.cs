using System.Text.Json;
using System.Text.Json.Serialization;
using Complot.Domain.Catalog;
using Complot.Domain.Common;
using Complot.Domain.Garden;
using Complot.Domain.Plantings;
using Complot.Domain.Tools;
using Complot.Domain.Work;
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
    private sealed record SeedPerson(string Key, string Name, string Color);
    private sealed record SeedPlantingRef(string Bed, string Variety, string? Cultivar, DateOnly SowOn);
    private sealed record SeedNote(string? Bed, string? Tree, SeedPlantingRef? Planting, string? Person, DateOnly Date, NoteTag Tag, string Text, decimal? HarvestQuantity, string? HarvestUnit);
    private sealed record SeedManualTask(string Title, int OffsetDays, int BaseMinutes, string? Bed, string? Person);
    private sealed record SeedToolLog(DateOnly Date, string? Text, bool IsMaintenance);
    private sealed record SeedTool(
        string Key, string Name, string Kind, string Icon, string Unit, int QtyGood, int QtyRepair, int QtyWorn, string? Material, bool IsReused,
        DateOnly? BoughtOn, decimal? UnitPrice, string? SupplierName, int? LifespanYears, int? MaintenanceIntervalMonths, int MaintenanceMinutes,
        List<string> Beds, string? Notes, List<SeedToolLog> Log);
    private sealed record SeedToolNeed(string Variety, string? Cultivar, string Tool, NeedPer Per, decimal Quantity);
    private sealed record SeedFile(
        List<SeedSite> Sites, List<SeedPlanting> Plantings, List<SeedPerson> People, List<SeedNote> Notes, List<SeedManualTask> ManualTasks,
        List<SeedTool> Tools, List<SeedToolNeed> ToolNeeds);

    /// <summary>How much longer (or shorter) than estimated each kind of task took in the sample history.</summary>
    private static readonly Dictionary<TaskKind, double> HistoryBias = new()
    {
        [TaskKind.SowIndoor] = 1.1, [TaskKind.Sow] = 0.9, [TaskKind.PlantOut] = 1.2, [TaskKind.Plant] = 1.2,
        [TaskKind.Harvest] = 1.0, [TaskKind.Clear] = 1.35, [TaskKind.Care] = 1.2, [TaskKind.Custom] = 1.05,
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static async Task AddToAsync(ComplotDbContext db, DateOnly today, CancellationToken ct)
    {
        await using var stream = typeof(DemoGarden).Assembly.GetManifestResourceStream("Complot.Api.Data.Seed.demo-garden.json")
            ?? throw new InvalidOperationException("Embedded demo-garden.json not found");
        var seed = await JsonSerializer.DeserializeAsync<SeedFile>(stream, Json, ct) ?? throw new InvalidOperationException("demo-garden.json is empty");

        var bedsByCode = new Dictionary<string, Bed>();
        var trees = new Dictionary<string, Tree>();
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
            foreach (var t in s.Trees)
            {
                trees[t.Name] = new Tree
                {
                    SiteId = site.Id, Name = t.Name, Cultivar = t.Cultivar, Kind = t.Kind, X = t.X, Y = t.Y,
                    CrownDiameter = t.CrownDiameter, PlantedOn = t.PlantedOn, Actions = t.Actions,
                };
                db.Trees.Add(trees[t.Name]);
            }
        }

        var varieties = await db.Varieties.ToDictionaryAsync(v => (v.Name, v.Cultivar), ct);
        var plantings = new Dictionary<SeedPlantingRef, Planting>();
        foreach (var p in seed.Plantings)
        {
            var variety = varieties[(p.Variety, p.Cultivar)];
            var bed = bedsByCode[p.Bed];
            var planting = new Planting { BedId = bed.Id, Bed = bed, VarietyId = variety.Id, Variety = variety, Share = p.Share };
            planting.ApplyDates(new PlantingDates(p.SowOn, p.PlantOutOn, p.HarvestFrom, p.HarvestTo, p.ClearOn));
            plantings[new SeedPlantingRef(p.Bed, p.Variety, p.Cultivar, p.SowOn)] = planting;
            db.Plantings.Add(planting);
        }

        var tools = seed.Tools.ToDictionary(t => t.Key, t => new Tool
        {
            Name = t.Name, Kind = t.Kind, Icon = t.Icon, Unit = t.Unit, QtyGood = t.QtyGood, QtyRepair = t.QtyRepair, QtyWorn = t.QtyWorn,
            Material = t.Material, IsReused = t.IsReused, BoughtOn = t.BoughtOn, UnitPrice = t.UnitPrice, SupplierName = t.SupplierName,
            LifespanYears = t.LifespanYears, MaintenanceIntervalMonths = t.MaintenanceIntervalMonths, MaintenanceMinutes = t.MaintenanceMinutes,
            BedIds = t.Beds.Select(code => bedsByCode[code].Id).ToList(), Notes = t.Notes,
            Log = t.Log.Select(l => new ToolLogEntry { Date = l.Date, Text = l.Text, IsMaintenance = l.IsMaintenance }).ToList(),
        });
        db.Tools.AddRange(tools.Values);
        foreach (var need in seed.ToolNeeds)
            varieties[(need.Variety, need.Cultivar)].ToolNeeds.Add(new ToolNeed { ToolId = tools[need.Tool].Id, Per = need.Per, Quantity = need.Quantity });

        var people = seed.People.ToDictionary(p => p.Key, p => new Person { Name = p.Name, Color = p.Color });
        db.People.AddRange(people.Values);
        Guid? PersonId(string? key) => key is null ? null : people[key].Id;

        foreach (var n in seed.Notes)
        {
            var bed = n.Bed is null ? null : bedsByCode[n.Bed];
            var tree = n.Tree is null ? null : trees[n.Tree];
            db.Notes.Add(new Note
            {
                SiteId = bed?.SiteId ?? tree!.SiteId, BedId = bed?.Id, TreeId = tree?.Id,
                PlantingId = n.Planting is null ? null : plantings[n.Planting].Id, PersonId = PersonId(n.Person),
                Date = n.Date, Tag = n.Tag, Text = n.Text, HarvestQuantity = n.HarvestQuantity, HarvestUnit = n.HarvestUnit,
            });
        }

        var home = bedsByCode.Values.First().SiteId;
        foreach (var m in seed.ManualTasks)
        {
            var task = new ManualTask
            {
                Title = m.Title, DueOn = today.AddDays(m.OffsetDays), BaseMinutes = m.BaseMinutes,
                SiteId = m.Bed is null ? home : bedsByCode[m.Bed].SiteId, BedId = m.Bed is null ? null : bedsByCode[m.Bed].Id,
            };
            db.ManualTasks.Add(task);
            db.TaskStates.Add(new TaskState { SourceKey = task.SourceKey, AssigneeId = PersonId(m.Person) });
        }

        AddWorkHistory(db, plantings.Values, trees.Values, people.Values.ToList(), today);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Marks past work as done with a plausible actual duration, so the estimates have something to learn from.</summary>
    private static void AddWorkHistory(ComplotDbContext db, IEnumerable<Planting> plantings, IEnumerable<Tree> trees, List<Person> people, DateOnly today)
    {
        var cutoff = today.AddDays(-14);
        var past = plantings.SelectMany(p => TaskGenerator.ForPlanting(p, cutoff.Year - 2, cutoff.Year))
            .Concat(trees.SelectMany(t => TaskGenerator.ForTree(t, cutoff.Year - 2, cutoff.Year)))
            .Where(t => t.Date < cutoff);
        foreach (var task in past)
        {
            var spread = StableFraction(task.Key);
            var state = new TaskState { SourceKey = task.Key, AssigneeId = people[(int)(spread * 2) % people.Count].Id };
            var actual = (int)Math.Round(task.BaseMinutes * HistoryBias[task.Kind] * (0.85 + spread * 0.3) / 5) * 5;
            state.Complete(task, task.Date, Math.Max(5, actual));
            db.TaskStates.Add(state);
        }
    }

    /// <summary>Deterministic 0–1 value per key (FNV-1a), so the sample history is the same on every seed.</summary>
    private static double StableFraction(string key)
    {
        var hash = 2166136261u;
        foreach (var c in key) hash = (hash ^ c) * 16777619u;
        return hash / (double)uint.MaxValue;
    }
}
