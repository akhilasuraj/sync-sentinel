namespace SyncSentinel.Core;

/// <summary>
/// Owns the live <see cref="SyncSentinelConfig"/> in memory, backed by a
/// <see cref="ConfigStore"/>. Loads on construction (seeding on first run) and
/// persists after every mutation. CRUD for jobs and exclusion sets, plus
/// settings updates and resolving a job to its effective <see cref="BackupJob"/>.
/// The id-keyed CRUD invariants live in <see cref="KeyedCollection"/>; each public
/// method is a thin, named pass-through naming its collection. Mutations are
/// serialized so concurrent API calls stay consistent.
/// </summary>
public sealed class ConfigService
{
    private readonly ConfigStore _store;
    private readonly object _gate = new();
    private SyncSentinelConfig _config;

    public ConfigService(ConfigStore store)
    {
        _store = store;
        _config = store.Load();
    }

    public SyncSentinelConfig Current
    {
        get { lock (_gate) { return _config; } }
    }

    // ── Jobs ────────────────────────────────────────────────────────────────

    public Job AddJob(Job job) => Add(job, c => c.Jobs, (c, x) => c with { Jobs = x });
    public bool UpdateJob(Job job) => Update(job, c => c.Jobs, (c, x) => c with { Jobs = x });
    public bool DeleteJob(string id) => Delete<Job>(id, c => c.Jobs, (c, x) => c with { Jobs = x });

    public BackupJob? ResolveJob(string id)
    {
        lock (_gate)
        {
            var job = _config.Jobs.FirstOrDefault(j => j.Id == id);
            return job is null ? null : JobResolver.Resolve(job, _config);
        }
    }

    // ── Folder exclusion sets ────────────────────────────────────────────────

    public FolderExclusionSet AddFolderSet(FolderExclusionSet set) => Add(set, c => c.FolderSets, (c, x) => c with { FolderSets = x });
    public bool UpdateFolderSet(FolderExclusionSet set) => Update(set, c => c.FolderSets, (c, x) => c with { FolderSets = x });
    public bool DeleteFolderSet(string id) => Delete<FolderExclusionSet>(id, c => c.FolderSets, (c, x) => c with { FolderSets = x });

    // ── File exclusion sets ──────────────────────────────────────────────────

    public FileExclusionSet AddFileSet(FileExclusionSet set) => Add(set, c => c.FileSets, (c, x) => c with { FileSets = x });
    public bool UpdateFileSet(FileExclusionSet set) => Update(set, c => c.FileSets, (c, x) => c with { FileSets = x });
    public bool DeleteFileSet(string id) => Delete<FileExclusionSet>(id, c => c.FileSets, (c, x) => c with { FileSets = x });

    // ── Settings ─────────────────────────────────────────────────────────────

    public void UpdateSettings(GlobalSettings settings)
    {
        lock (_gate)
        {
            Mutate(_config with { Settings = settings });
        }
    }

    // ── internals ────────────────────────────────────────────────────────────

    // Each CRUD pair is the same shape over a different collection: read the
    // current list (under the gate), apply the keyed-collection invariant, then
    // persist via the caller's setter. The named public methods just supply the
    // get/set pair, so a new collection type costs three one-liners, not three
    // hand-written method bodies.
    private T Add<T>(
        T item,
        Func<SyncSentinelConfig, IReadOnlyList<T>> get,
        Func<SyncSentinelConfig, IReadOnlyList<T>, SyncSentinelConfig> set)
        where T : IIdentified<T>
    {
        lock (_gate)
        {
            var (items, added) = KeyedCollection.Add(get(_config), item, NewId);
            Mutate(set(_config, items));
            return added;
        }
    }

    private bool Update<T>(
        T item,
        Func<SyncSentinelConfig, IReadOnlyList<T>> get,
        Func<SyncSentinelConfig, IReadOnlyList<T>, SyncSentinelConfig> set)
        where T : IIdentified<T> =>
        Apply(c => KeyedCollection.Update(get(c), item), set);

    private bool Delete<T>(
        string id,
        Func<SyncSentinelConfig, IReadOnlyList<T>> get,
        Func<SyncSentinelConfig, IReadOnlyList<T>, SyncSentinelConfig> set)
        where T : IIdentified<T> =>
        Apply(c => KeyedCollection.Delete(get(c), id), set);

    // Read-modify-write under the gate: compute the next list from the current
    // config; false (not-found) when nothing changed, otherwise persist. Keeping
    // the read and the write inside one lock keeps concurrent CRUD consistent.
    private bool Apply<T>(
        Func<SyncSentinelConfig, IReadOnlyList<T>?> change,
        Func<SyncSentinelConfig, IReadOnlyList<T>, SyncSentinelConfig> set)
    {
        lock (_gate)
        {
            var next = change(_config);
            if (next is null)
            {
                return false;
            }
            Mutate(set(_config, next));
            return true;
        }
    }

    // Caller holds _gate. Swap the in-memory config and persist it.
    private void Mutate(SyncSentinelConfig next)
    {
        _config = next;
        _store.Save(_config);
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
