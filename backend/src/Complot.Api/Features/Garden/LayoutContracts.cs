using Complot.Api.Common;
using Complot.Domain.Access;
using Complot.Domain.Catalog;
using Complot.Domain.Garden;

namespace Complot.Api.Features.Garden;

/// <summary>A site as the caller sees it: <see cref="MyRole"/> is what the caller may do there.</summary>
public sealed record SiteDto(Guid Id, string Name, string? Notes, int BedCount, int MemberCount, bool Archived, SiteRole MyRole);

public sealed record SiteInput(string Name, string? Notes);

public sealed record ShapeDto(ShapeKind Kind, double X, double Y, double Width, double Height);

public sealed record PointDto(double X, double Y);

/// <summary>A plot; <see cref="MapItem.Layer"/> says how every map item is stacked.</summary>
public sealed record PlotDto(Guid Id, string Name, PlotType Type, ShapeDto Shape, int Layer = 0);

public sealed record BedDto(Guid Id, Guid PlotId, string Code, BedKind Kind, ShapeDto Shape, SunExposure Sun, string? Soil, string? Notes, double Area, int Layer = 0);

/// <summary>A path through <see cref="Points"/> (at least two, along its middle), <see cref="Width"/> m wide.</summary>
public sealed record PathDto(Guid Id, string Name, PathMaterial Material, List<PointDto> Points, double Width, int Layer = 0);

public sealed record FeatureDto(Guid Id, string Name, FeatureType Type, ShapeDto Shape, int Layer = 0);

/// <summary>
/// A reference area or line (see <see cref="MapMarker"/>): an area has a <see cref="Shape"/>, a line (wall, fence) runs
/// through <see cref="Points"/> (at least two).
/// </summary>
public sealed record MarkerDto(Guid Id, MarkerKind Kind, string? Label, string Color, ShapeDto? Shape, List<PointDto>? Points, double Thickness, int Layer = 0);

/// <summary>
/// A tree or shrub (see <see cref="Tree"/>): its crown is an ellipse or a rect (espalier) around the trunk at (x, y).
/// With a <see cref="VarietyId"/> the variety's actions and pollination apply; <see cref="Actions"/> are its own on top,
/// and <see cref="Pollination"/> only counts when the variety has none. <see cref="Pollinators"/> is calculated
/// (ignored on save): the trees and plantings within range that can pollinate it from today on, nearest first; null
/// when it needs none, empty when it lacks one.
/// </summary>
public sealed record TreeDto(
    Guid Id, string Name, string? Cultivar, TreeKind Kind, Guid? VarietyId, double X, double Y, ShapeKind Outline,
    double CrownWidth, double CrownDepth, DateOnly? PlantedOn, List<YearlyActionDto> Actions, PollinationDto? Pollination = null,
    List<PollinatorDto>? Pollinators = null, int Layer = 0);

/// <summary>A tree or planting that pollinates another one, <see cref="Distance"/> m away.</summary>
public sealed record PollinatorDto(Guid Id, string Name, double Distance);

/// <summary>
/// Everything drawn on a site's map. The designer edits a local copy (with undo/redo) and saves the whole layout at
/// once; items keep their ids (new items get a client-generated id). <see cref="BedDto.Area"/> is ignored on save.
/// </summary>
public sealed record SiteLayoutDto(
    List<PlotDto> Plots, List<BedDto> Beds, List<PathDto> Paths, List<FeatureDto> Features, List<TreeDto> Trees,
    List<MarkerDto> Markers);

public static class LayoutMapping
{
    public static ShapeDto ToDto(this Shape s) => new(s.Kind, s.X, s.Y, s.Width, s.Height);
    public static Shape ToDomain(this ShapeDto s) => new(s.Kind, s.X, s.Y, s.Width, s.Height);
    public static List<PointDto> ToDtos(this IEnumerable<MapPoint> points) => [.. points.Select(p => new PointDto(p.X, p.Y))];
    public static List<MapPoint> ToDomain(this IEnumerable<PointDto> points) => [.. points.Select(p => new MapPoint { X = p.X, Y = p.Y })];

    public static PlotDto ToDto(this Plot p) => new(p.Id, p.Name, p.Type, p.Shape.ToDto(), p.Layer);
    public static BedDto ToDto(this Bed b) => new(b.Id, b.PlotId, b.Code, b.Kind, b.Shape.ToDto(), b.Sun, b.Soil, b.Notes, Math.Round(b.Shape.Area, 2), b.Layer);
    public static PathDto ToDto(this GardenPath p) => new(p.Id, p.Name, p.Material, p.Points.ToDtos(), p.Width, p.Layer);
    public static FeatureDto ToDto(this GardenFeature f) => new(f.Id, f.Name, f.Type, f.Shape.ToDto(), f.Layer);
    public static MarkerDto ToDto(this MapMarker m) => m.Kind == MarkerKind.Line
        ? new(m.Id, m.Kind, m.Label, m.Color, null, m.Points.ToDtos(), m.Thickness, m.Layer)
        : new(m.Id, m.Kind, m.Label, m.Color, new ShapeDto(m.Outline, m.X, m.Y, m.Width, m.Height), null, m.Thickness, m.Layer);
    public static TreeDto ToDto(this Tree t, List<PollinatorDto>? pollinators = null) =>
        new(t.Id, t.Name, t.Cultivar, t.Kind, t.VarietyId, t.X, t.Y, t.Outline, t.CrownWidth, t.CrownDepth, t.PlantedOn,
            [.. t.Actions.ToDtos()], t.Pollination.ToDto(), pollinators, t.Layer);
}
