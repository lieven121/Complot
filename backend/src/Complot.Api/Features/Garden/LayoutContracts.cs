using Complot.Api.Common;
using Complot.Domain.Access;
using Complot.Domain.Catalog;
using Complot.Domain.Garden;

namespace Complot.Api.Features.Garden;

/// <summary>A site as the caller sees it: <see cref="MyRole"/> is what the caller may do there.</summary>
public sealed record SiteDto(Guid Id, string Name, string? Notes, int BedCount, int MemberCount, bool Archived, SiteRole MyRole);

public sealed record SiteInput(string Name, string? Notes);

public sealed record ShapeDto(ShapeKind Kind, double X, double Y, double Width, double Height);

public sealed record PlotDto(Guid Id, string Name, PlotType Type, ShapeDto Shape);

public sealed record BedDto(Guid Id, Guid PlotId, string Code, BedKind Kind, ShapeDto Shape, SunExposure Sun, string? Soil, string? Notes, double Area);

public sealed record PathDto(Guid Id, string Name, PathMaterial Material, ShapeDto Shape);

public sealed record FeatureDto(Guid Id, string Name, FeatureType Type, ShapeDto Shape);

/// <summary>
/// A reference area or line (see <see cref="MapMarker"/>). The shape is the area's box, or a line from (x, y) by
/// (width, height), which may be zero or negative.
/// </summary>
public sealed record MarkerDto(Guid Id, MarkerKind Kind, string? Label, string Color, ShapeDto Shape, double Thickness);

/// <summary>
/// A tree or shrub. <see cref="Pollinators"/> is calculated (ignored on save): the trees and plantings within range
/// that can pollinate it from today on, nearest first; null when it needs none, empty when it lacks one.
/// </summary>
public sealed record TreeDto(
    Guid Id, string Name, string? Cultivar, TreeKind Kind, double X, double Y, double CrownDiameter, DateOnly? PlantedOn,
    List<YearlyActionDto> Actions, PollinationDto? Pollination = null, List<PollinatorDto>? Pollinators = null);

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

    public static PlotDto ToDto(this Plot p) => new(p.Id, p.Name, p.Type, p.Shape.ToDto());
    public static BedDto ToDto(this Bed b) => new(b.Id, b.PlotId, b.Code, b.Kind, b.Shape.ToDto(), b.Sun, b.Soil, b.Notes, Math.Round(b.Shape.Area, 2));
    public static PathDto ToDto(this GardenPath p) => new(p.Id, p.Name, p.Material, p.Shape.ToDto());
    public static FeatureDto ToDto(this GardenFeature f) => new(f.Id, f.Name, f.Type, f.Shape.ToDto());
    public static MarkerDto ToDto(this MapMarker m) =>
        new(m.Id, m.Kind, m.Label, m.Color, new ShapeDto(m.Outline, m.X, m.Y, m.Width, m.Height), m.Thickness);
    public static TreeDto ToDto(this Tree t, List<PollinatorDto>? pollinators = null) =>
        new(t.Id, t.Name, t.Cultivar, t.Kind, t.X, t.Y, t.CrownDiameter, t.PlantedOn, [.. t.Actions.ToDtos()], t.Pollination.ToDto(), pollinators);
}
