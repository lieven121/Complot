using Complot.Domain.Common;

namespace Complot.Domain.Catalog;

public sealed class Supplier : Entity
{
    public required string Name { get; set; }
    public string? Url { get; set; }

    /// <summary>Own seed, own harvest or cuttings — the circular "supplier".</summary>
    public bool IsOwnSource { get; set; }

    public string? Notes { get; set; }
}
