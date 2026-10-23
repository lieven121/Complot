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

    /// <summary>
    /// Makes the stored set match <paramref name="wanted"/> by id: deletes items that are gone, updates the others in
    /// place and adds new ones (keeping the id the client chose).
    /// </summary>
    public static void SyncById<TEntity, TDto>(
        this DbSet<TEntity> set, IReadOnlyCollection<TEntity> existing, IEnumerable<TDto> wanted,
        Func<TDto, Guid> id, Func<TDto, TEntity> create, Action<TDto, TEntity> apply)
        where TEntity : Entity
    {
        var wantedById = wanted.ToDictionary(id);
        set.RemoveRange(existing.Where(e => !wantedById.ContainsKey(e.Id)));
        var existingById = existing.ToDictionary(e => e.Id);
        foreach (var (key, dto) in wantedById)
        {
            if (existingById.TryGetValue(key, out var entity))
            {
                apply(dto, entity);
                continue;
            }
            var created = create(dto);
            apply(dto, created);
            set.Add(created);
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
