using Complot.Domain.Catalog;
using Complot.Domain.Common;

namespace Complot.Domain.Garden;

/// <summary>A garden location (e.g. home, allotment). Everything on the map belongs to one site.</summary>
public sealed class Site : Entity
{
    public required string Name { get; set; }
    public string? Notes { get; set; }
}

public enum PlotType
{
    Open,
    Greenhouse,
    Orchard,
}

public sealed class Plot : Entity
{
    public Guid SiteId { get; set; }
    public required string Name { get; set; }
    public PlotType Type { get; set; }
    public required Shape Shape { get; set; }
}

public sealed class Bed : Entity
{
    /// <summary>Beds closer than this (same plot) count as neighbours for companion planting.</summary>
    public const double NeighbourDistanceMeters = 0.6;

    public Guid SiteId { get; set; }
    public Guid PlotId { get; set; }
    public required string Code { get; set; }
    public required Shape Shape { get; set; }
    public SunExposure Sun { get; set; } = SunExposure.Full;
    public string? Soil { get; set; }
    public string? Notes { get; set; }

    public bool IsNeighbourOf(Bed other) =>
        other.Id != Id && other.PlotId == PlotId && Shape.DistanceTo(other.Shape) <= NeighbourDistanceMeters;
}

public enum PathMaterial
{
    WoodChips,
    Grass,
    Pavers,
    Gravel,
}

public sealed class GardenPath : Entity
{
    public Guid SiteId { get; set; }
    public required string Name { get; set; }
    public PathMaterial Material { get; set; }
    public required Shape Shape { get; set; }
}

public enum FeatureType
{
    Compost,
    Water,
    Pond,
    Hedge,
    Shed,
}

/// <summary>Non-growing map element: compost bins, rain water, pond, hedge, shed.</summary>
public sealed class GardenFeature : Entity
{
    public Guid SiteId { get; set; }
    public required string Name { get; set; }
    public FeatureType Type { get; set; }
    public required Shape Shape { get; set; }
}

public enum TreeKind
{
    Tree,
    Shrub,
}

/// <summary>A tree or shrub, drawn as its crown around (X, Y). Its yearly actions become tasks.</summary>
public sealed class Tree : Entity
{
    public Guid SiteId { get; set; }
    public required string Name { get; set; }
    public string? Cultivar { get; set; }
    public TreeKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double CrownDiameter { get; set; }
    public DateOnly? PlantedOn { get; set; }
    public List<YearlyAction> Actions { get; set; } = [];
}
