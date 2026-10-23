namespace Complot.Domain.Common;

/// <summary>Recurring yearly care (e.g. winter pruning in February) for perennials and trees; each one becomes a task.</summary>
public sealed class YearlyAction
{
    public required string Title { get; set; }
    public int Month { get; set; }
    public int EstimatedMinutes { get; set; }
}
