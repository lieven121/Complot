namespace Complot.Api.Common;

public static class CollectionSync
{
    /// <summary>
    /// Makes <paramref name="target"/> match <paramref name="source"/> by key: removes missing items, updates
    /// existing ones in place and adds new ones. Keeps EF Core change tracking intact for keyed child collections
    /// (replacing the whole list would re-insert rows with the same key).
    /// </summary>
    public static void SyncWith<TItem, TSource, TKey>(
        this List<TItem> target,
        IEnumerable<TSource> source,
        Func<TItem, TKey> itemKey,
        Func<TSource, TKey> sourceKey,
        Func<TSource, TItem> create,
        Action<TSource, TItem>? update = null)
        where TKey : notnull
    {
        var wanted = source.ToDictionary(sourceKey);
        target.RemoveAll(item => !wanted.ContainsKey(itemKey(item)));
        var existing = target.ToDictionary(itemKey);
        foreach (var (key, value) in wanted)
        {
            if (existing.TryGetValue(key, out var item)) update?.Invoke(value, item);
            else target.Add(create(value));
        }
    }
}
