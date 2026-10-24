using Complot.Domain.Common;
using Complot.Domain.Plantings;

namespace Complot.Domain.Tools;

/// <summary>
/// Tools and materials, counted as quantities split by condition. A maintenance interval turns into tasks; a lifespan
/// gives a replacement date.
/// </summary>
public sealed class Tool : Entity
{
    public required string Name { get; set; }

    /// <summary>Free grouping, e.g. "Irrigatie", "Handgereedschap".</summary>
    public required string Kind { get; set; }

    public string Icon { get; set; } = "🧰";
    public string Unit { get; set; } = "stuks";

    public int QtyGood { get; set; }
    public int QtyRepair { get; set; }
    public int QtyWorn { get; set; }

    public string? Material { get; set; }

    /// <summary>Second-hand or made from reused material (circular).</summary>
    public bool IsReused { get; set; }

    public DateOnly? BoughtOn { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? SupplierName { get; set; }
    public int? LifespanYears { get; set; }

    public int? MaintenanceIntervalMonths { get; set; }
    public int MaintenanceMinutes { get; set; } = 30;

    /// <summary>Beds where (part of) it is in use; empty = in storage.</summary>
    public List<Guid> BedIds { get; set; } = [];

    public string? Notes { get; set; }
    public List<ToolLogEntry> Log { get; set; } = [];

    public int Quantity => QtyGood + QtyRepair + QtyWorn;

    public DateOnly? LastMaintenance => Log.Where(e => e.IsMaintenance).Select(e => (DateOnly?)e.Date).Max();

    /// <summary>Next maintenance: one interval after the last one (or after buying it, or after it was added).</summary>
    public DateOnly? NextMaintenance =>
        MaintenanceIntervalMonths is { } months ? (LastMaintenance ?? BoughtOn ?? DateOnly.FromDateTime(CreatedAt)).AddMonths(months) : null;

    public DateOnly? ReplaceBy => BoughtOn is { } bought && LifespanYears is { } years ? bought.AddYears(years) : null;
}

/// <summary>What happened to a tool: repairs, maintenance (sets the next due date), costs.</summary>
public sealed class ToolLogEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateOnly Date { get; set; }
    public string? Text { get; set; }
    public decimal? Cost { get; set; }
    public bool IsMaintenance { get; set; }

    /// <summary>Set when the entry was made by checking off a maintenance task (and removed when it is reopened).</summary>
    public string? TaskKey { get; set; }
}

public enum NeedPer
{
    Plant,
    Bed,
}

/// <summary>A variety needs this much of a tool while it is in a bed, e.g. 1 spiral per plant or 1 net per bed.</summary>
public sealed class ToolNeed
{
    public Guid ToolId { get; set; }
    public NeedPer Per { get; set; }
    public decimal Quantity { get; set; } = 1;
}

/// <summary>The most of a tool the plan needs at once during a year, and in which month.</summary>
public sealed record ToolPeak(Guid ToolId, int Quantity, int Month);

public static class ToolDemand
{
    /// <summary>
    /// Per tool, the peak need over the months of <paramref name="year"/> (counted mid-month) from the plantings that are
    /// in a bed then. Each planting's need is rounded up: 2.5 spirals means 3.
    /// </summary>
    public static IReadOnlyList<ToolPeak> PeaksIn(IEnumerable<Planting> plantings, int year)
    {
        var list = plantings.Where(p => p.Variety.ToolNeeds.Count > 0).ToList();
        var peaks = new Dictionary<Guid, ToolPeak>();
        for (var month = 1; month <= 12; month++)
        {
            var day = new DateOnly(year, month, 15);
            var needed = new Dictionary<Guid, int>();
            foreach (var planting in list.Where(p => p.StartInBed <= day && day <= p.EndInBed))
            foreach (var need in planting.Variety.ToolNeeds)
            {
                var amount = need.Per == NeedPer.Bed ? need.Quantity : need.Quantity * (planting.PlantCount() ?? 1);
                needed[need.ToolId] = needed.GetValueOrDefault(need.ToolId) + (int)Math.Ceiling(amount);
            }
            foreach (var (tool, quantity) in needed)
                if (!peaks.TryGetValue(tool, out var peak) || quantity > peak.Quantity) peaks[tool] = new ToolPeak(tool, quantity, month);
        }
        return peaks.Values.ToList();
    }
}
