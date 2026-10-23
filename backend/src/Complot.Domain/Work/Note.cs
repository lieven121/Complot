using Complot.Domain.Common;

namespace Complot.Domain.Work;

public enum NoteTag
{
    Observation,
    Disease,
    Pest,
    Harvest,
    Soil,
    Weather,
}

/// <summary>Log entry for a bed, tree or crop: what was seen or done, optionally a harvest quantity and a photo.</summary>
public sealed class Note : Entity
{
    public Guid SiteId { get; set; }
    public Guid? BedId { get; set; }
    public Guid? TreeId { get; set; }
    public Guid? PlantingId { get; set; }
    public Guid? PersonId { get; set; }
    public DateOnly Date { get; set; }
    public NoteTag Tag { get; set; }
    public required string Text { get; set; }
    public decimal? HarvestQuantity { get; set; }
    public string? HarvestUnit { get; set; }
    public string? PhotoFileName { get; set; }
}
