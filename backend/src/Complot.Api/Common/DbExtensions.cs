using Complot.Domain.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Common;

public static class DbExtensions
{
    private const int SqliteConstraint = 19;

    public static Task<bool> ExistsAsync<T>(this DbSet<T> set, Guid id, CancellationToken cancellationToken) where T : Entity =>
        set.AnyAsync(e => e.Id == id, cancellationToken);

    /// <summary>Saves; a unique-index violation becomes a 409 with <paramref name="duplicateCode"/> instead of a 500.</summary>
    public static async Task<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult?> SaveOrConflictAsync(
        this DbContext db, string duplicateCode, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: SqliteConstraint } sqlite
                                           && sqlite.Message.Contains("UNIQUE", StringComparison.Ordinal))
        {
            return ApiProblems.Conflict(duplicateCode);
        }
    }

    /// <summary>Escapes LIKE wildcards in user input and wraps it for a "contains" match.</summary>
    public static string ContainsPattern(string term) =>
        "%" + term.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
}

public static class TimeProviderExtensions
{
    public static DateOnly Today(this TimeProvider time) => DateOnly.FromDateTime(time.GetLocalNow().DateTime);
}
