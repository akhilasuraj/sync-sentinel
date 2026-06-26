namespace SyncSentinel.Core;

/// <summary>
/// Owns the live <see cref="SyncSentinelConfig"/> in memory, backed by a
/// <see cref="ConfigStore"/>. Loads on construction (seeding on first run) and
/// persists after every mutation. CRUD for jobs and exclusion sets, plus
/// settings updates and resolving a job to its effective <see cref="BackupJob"/>.
/// Mutations are serialized so concurrent API calls stay consistent.
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

    public Job AddJob(Job job)
    {
        lock (_gate)
        {
            var withId = string.IsNullOrEmpty(job.Id) ? job with { Id = NewId() } : job;
            Mutate(_config with { Jobs = [.. _config.Jobs, withId] });
            return withId;
        }
    }

    public bool UpdateJob(Job job)
    {
        lock (_gate)
        {
            if (!_config.Jobs.Any(j => j.Id == job.Id))
            {
                return false;
            }
            Mutate(_config with { Jobs = [.. _config.Jobs.Select(j => j.Id == job.Id ? job : j)] });
            return true;
        }
    }

    public bool DeleteJob(string id)
    {
        lock (_gate)
        {
            var kept = _config.Jobs.Where(j => j.Id != id).ToList();
            if (kept.Count == _config.Jobs.Count)
            {
                return false;
            }
            Mutate(_config with { Jobs = kept });
            return true;
        }
    }

    public BackupJob? ResolveJob(string id)
    {
        lock (_gate)
        {
            var job = _config.Jobs.FirstOrDefault(j => j.Id == id);
            return job is null ? null : JobResolver.Resolve(job, _config);
        }
    }

    // ── Folder exclusion sets ────────────────────────────────────────────────

    public FolderExclusionSet AddFolderSet(FolderExclusionSet set)
    {
        lock (_gate)
        {
            var withId = string.IsNullOrEmpty(set.Id) ? set with { Id = NewId() } : set;
            Mutate(_config with { FolderSets = [.. _config.FolderSets, withId] });
            return withId;
        }
    }

    public bool UpdateFolderSet(FolderExclusionSet set)
    {
        lock (_gate)
        {
            if (!_config.FolderSets.Any(s => s.Id == set.Id))
            {
                return false;
            }
            Mutate(_config with { FolderSets = [.. _config.FolderSets.Select(s => s.Id == set.Id ? set : s)] });
            return true;
        }
    }

    public bool DeleteFolderSet(string id)
    {
        lock (_gate)
        {
            var kept = _config.FolderSets.Where(s => s.Id != id).ToList();
            if (kept.Count == _config.FolderSets.Count)
            {
                return false;
            }
            Mutate(_config with { FolderSets = kept });
            return true;
        }
    }

    // ── File exclusion sets ──────────────────────────────────────────────────

    public FileExclusionSet AddFileSet(FileExclusionSet set)
    {
        lock (_gate)
        {
            var withId = string.IsNullOrEmpty(set.Id) ? set with { Id = NewId() } : set;
            Mutate(_config with { FileSets = [.. _config.FileSets, withId] });
            return withId;
        }
    }

    public bool UpdateFileSet(FileExclusionSet set)
    {
        lock (_gate)
        {
            if (!_config.FileSets.Any(s => s.Id == set.Id))
            {
                return false;
            }
            Mutate(_config with { FileSets = [.. _config.FileSets.Select(s => s.Id == set.Id ? set : s)] });
            return true;
        }
    }

    public bool DeleteFileSet(string id)
    {
        lock (_gate)
        {
            var kept = _config.FileSets.Where(s => s.Id != id).ToList();
            if (kept.Count == _config.FileSets.Count)
            {
                return false;
            }
            Mutate(_config with { FileSets = kept });
            return true;
        }
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    public void UpdateSettings(GlobalSettings settings)
    {
        lock (_gate)
        {
            Mutate(_config with { Settings = settings });
        }
    }

    // ── internals ────────────────────────────────────────────────────────────

    // Caller holds _gate. Swap the in-memory config and persist it.
    private void Mutate(SyncSentinelConfig next)
    {
        _config = next;
        _store.Save(_config);
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
