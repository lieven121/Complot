using Complot.Domain.Common;

namespace Complot.Domain.Catalog;

/// <summary>A user-facing group of crops (e.g. "Tomaten"). Carries companion rules and the default family.</summary>
public sealed class PlantCategory : Entity
{
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public string Icon { get; set; } = "🌱";
    public string Color { get; set; } = "#3f7d4e";

    public Guid? DefaultFamilyId { get; set; }
    public PlantFamily? DefaultFamily { get; set; }

    /// <summary>Green manures and herbs are usually not part of the rotation.</summary>
    public bool RotationExempt { get; set; }

    public List<CategoryCompanion> Companions { get; set; } = [];

    public IEnumerable<Guid> CompanionIds(CompanionRelation relation) =>
        Companions.Where(c => c.Relation == relation).Select(c => c.OtherCategoryId);
}

/// <summary>Directed companion rule: plants of <see cref="CategoryId"/> do well / badly next to <see cref="OtherCategoryId"/>.</summary>
public sealed class CategoryCompanion
{
    public Guid CategoryId { get; set; }
    public Guid OtherCategoryId { get; set; }
    public CompanionRelation Relation { get; set; }
}
