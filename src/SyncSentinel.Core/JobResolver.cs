namespace SyncSentinel.Core;

/// <summary>
/// Flattens a persisted <see cref="Job"/> into the <see cref="BackupJob"/> the
/// robocopy runner consumes — the "effective command" (see CONTEXT.md): the
/// union of its attached folder-sets becomes the /XD list, the union of its
/// file-sets the /XF list, and its flags override (or the global default) the
/// behavior flags. Unknown set ids are ignored so a deleted set never breaks a run.
/// </summary>
public static class JobResolver
{
    public static BackupJob Resolve(Job job, SyncSentinelConfig config)
    {
        var folderSets = config.FolderSets.ToDictionary(s => s.Id);
        var fileSets = config.FileSets.ToDictionary(s => s.Id);

        var folders = Union(job.FolderSetIds, folderSets, s => s.Folders);
        var files = Union(job.FileSetIds, fileSets, s => s.Patterns);

        return new BackupJob
        {
            JobId = job.Id,
            Name = job.Name,
            Source = job.Source,
            Destination = job.Destination,
            Flags = job.FlagsOverride ?? config.Settings.DefaultFlags,
            ExcludeFolders = folders,
            ExcludeFiles = files,
        };
    }

    // Flatten the items of every referenced set into one ordered, de-duplicated
    // list. Unknown ids (e.g. a set deleted after the job referenced it) are skipped.
    private static IReadOnlyList<string> Union<TSet>(
        IReadOnlyList<string> setIds,
        IReadOnlyDictionary<string, TSet> sets,
        Func<TSet, IReadOnlyList<string>> items)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in setIds)
        {
            if (!sets.TryGetValue(id, out var set))
            {
                continue;
            }
            foreach (var item in items(set))
            {
                if (seen.Add(item))
                {
                    result.Add(item);
                }
            }
        }
        return result;
    }
}
