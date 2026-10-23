using Complot.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Garden;

/// <summary>A bed that has plantings or notes cannot be removed from the layout.</summary>
internal static class BedUsage
{
    public static async Task<bool> AnyInUseAsync(ComplotDbContext db, IReadOnlyCollection<Guid> bedIds, CancellationToken ct) =>
        bedIds.Count > 0 && await db.Plantings.AnyAsync(p => bedIds.Contains(p.BedId), ct);
}
