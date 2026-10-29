using Complot.Domain.Catalog;
using Complot.Domain.Common;

namespace Complot.Domain.Garden;

/// <summary>A garden location (e.g. home, allotment). Everything on the map belongs to one site.</summary>
public sealed class Site : Entity
{
    public required string Name { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set when the site is archived: hidden, read-only, and only then allowed to be deleted.</summary>
    public DateTime? ArchivedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;
}

/// <summary>
/// Something drawn on a site's map. <see cref="Layer"/> orders the drawing: higher lies on top. Plots are always below
/// everything else (their layer only orders them among each other); items on the same layer keep the default order
/// (markers, paths, features, beds, trees).
/// </summary>
public abstract class MapItem : Entity
{
    public Guid SiteId { get; set; }
    public int Layer { get; set; }
}

public enum PlotType
{
    Open,
    Greenhouse,
    Orchard,
}

public sealed class Plot : MapItem
{
    public required string Name { get; set; }
    public PlotType Type { get; set; }
    public required Shape Shape { get; set; }
}

/// <summary>
/// How crops are laid out in a bed: <see cref="Strips"/> gives each planting a share along the long side;
/// <see cref="Spots"/> places a counted number of plants at a point (small mixed beds).
/// </summary>
public enum BedKind
{
    Strips,
    Spots,
}

public sealed class Bed : MapItem
{
    /// <summary>Beds closer than this (same plot) count as neighbours for companion planting.</summary>
    public const double NeighbourDistanceMeters = 0.6;

    public Guid PlotId { get; set; }
    public required string Code { get; set; }
    public BedKind Kind { get; set; } = BedKind.Strips;
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

/// <summary>A path along a line through <see cref="Points"/> (its middle), <see cref="Width"/> metres wide, so it can turn corners.</summary>
public sealed class GardenPath : MapItem
{
    public required string Name { get; set; }
    public PathMaterial Material { get; set; }
    public List<MapPoint> Points { get; set; } = [];
    public double Width { get; set; } = 0.8;
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
public sealed class GardenFeature : MapItem
{
    public required string Name { get; set; }
    public FeatureType Type { get; set; }
    public required Shape Shape { get; set; }
}

public enum TreeKind
{
    Tree,
    Shrub,
}

/// <summary>
/// A tree or shrub, drawn as its crown around the trunk at (X, Y): round (<see cref="ShapeKind.Ellipse"/>) or, for an
/// espalier or a hedge-like shrub, a <see cref="ShapeKind.Rect"/> of <see cref="CrownWidth"/> (east–west) by
/// <see cref="CrownDepth"/>. When it is of a catalog <see cref="Variety"/>, that variety's yearly actions and
/// pollination apply; its own <see cref="Actions"/> come on top. Its yearly actions become tasks.
/// </summary>
public sealed class Tree : MapItem
{
    public required string Name { get; set; }
    public string? Cultivar { get; set; }
    public TreeKind Kind { get; set; }
    public Guid? VarietyId { get; set; }
    public PlantVariety? Variety { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public ShapeKind Outline { get; set; } = ShapeKind.Ellipse;
    public double CrownWidth { get; set; }
    public double CrownDepth { get; set; }
    public DateOnly? PlantedOn { get; set; }
    public List<YearlyAction> Actions { get; set; } = [];

    /// <summary>Null when it needs no pollinator and pollinates nothing (e.g. most ornamentals).</summary>
    public Pollination? Pollination { get; set; }

    /// <summary>
    /// The variety's actions (needs <see cref="Variety"/> loaded) and its own; an own action with the same title replaces
    /// the variety's (e.g. pruning a month later).
    /// </summary>
    public IEnumerable<YearlyAction> AllActions =>
        (Variety?.Actions ?? []).Where(v => !Actions.Any(a => string.Equals(a.Title.Trim(), v.Title.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Concat(Actions);

    /// <summary>The variety's pollination when it has one (needs <see cref="Variety"/> loaded), else its own.</summary>
    public Pollination? EffectivePollination => Variety?.Pollination ?? Pollination;

    public string? EffectiveCultivar => Cultivar ?? Variety?.Cultivar;
}

public enum MarkerKind
{
    Area,
    Line,
}

/// <summary>
/// A reference drawing on the map without any rules, e.g. a terrace, a wall or a fence. An <see cref="MarkerKind.Area"/>
/// is a coloured rectangle or ellipse (<see cref="Outline"/>) with its top-left corner at (X, Y); a
/// <see cref="MarkerKind.Line"/> runs through <see cref="Points"/> (so it can turn corners) and is drawn
/// <see cref="Thickness"/> metres wide.
/// </summary>
public sealed class MapMarker : MapItem
{
    public MarkerKind Kind { get; set; }
    public string? Label { get; set; }
    public required string Color { get; set; }
    public ShapeKind Outline { get; set; } = ShapeKind.Rect;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Thickness { get; set; } = 0.2;
    public List<MapPoint> Points { get; set; } = [];
}

/// <summary>A point on the site map in metres (x → east, y → south).</summary>
public sealed class MapPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}
