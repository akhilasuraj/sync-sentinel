namespace SyncSentinel.Core;

/// <summary>
/// A config entity with a server-assigned id that can clone itself with one.
/// Implemented by the persisted records (Job + the exclusion sets) so the shared
/// <see cref="KeyedCollection"/> CRUD can read and assign ids generically.
/// </summary>
public interface IIdentified<T> where T : IIdentified<T>
{
    string Id { get; }
    T WithId(string id);
}

/// <summary>
/// The pure CRUD invariants every id-keyed config collection shares: assign an id
/// on add when one isn't supplied, replace the element with a matching id, and
/// remove by id. Operates on (and returns) immutable lists; <c>null</c> means
/// "no element with that id" so the caller can report not-found. ConfigService's
/// named Add/Update/Delete methods are thin pass-throughs over these.
/// </summary>
public static class KeyedCollection
{
    /// <summary>Append <paramref name="item"/>, assigning a fresh id when it has none.</summary>
    public static (IReadOnlyList<T> Items, T Added) Add<T>(IReadOnlyList<T> items, T item, Func<string> newId)
        where T : IIdentified<T>
    {
        var withId = string.IsNullOrEmpty(item.Id) ? item.WithId(newId()) : item;
        return ([.. items, withId], withId);
    }

    /// <summary>Replace the element whose id matches <paramref name="item"/>; null if none.</summary>
    public static IReadOnlyList<T>? Update<T>(IReadOnlyList<T> items, T item)
        where T : IIdentified<T>
    {
        if (!items.Any(x => x.Id == item.Id))
        {
            return null;
        }
        return [.. items.Select(x => x.Id == item.Id ? item : x)];
    }

    /// <summary>Remove the element with id <paramref name="id"/>; null if none matched.</summary>
    public static IReadOnlyList<T>? Delete<T>(IReadOnlyList<T> items, string id)
        where T : IIdentified<T>
    {
        var kept = items.Where(x => x.Id != id).ToList();
        return kept.Count == items.Count ? null : kept;
    }
}
