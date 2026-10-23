namespace Complot.Domain.Common;

/// <summary>Base class for aggregate roots and entities: time-ordered id and audit timestamps (UTC).</summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
