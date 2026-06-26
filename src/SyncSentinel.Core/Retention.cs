namespace SyncSentinel.Core;

/// <summary>
/// The pure retention policy: a run is kept only if it is among the most recent
/// <see cref="RetentionSettings.RunsPerJob"/> for its job AND finished within the
/// last <see cref="RetentionSettings.Days"/> days. Anything failing either cap is
/// expired. Counting is per job, not global.
/// </summary>
public static class Retention
{
    /// <summary>Return the ids of runs that should be pruned.</summary>
    public static IReadOnlyList<string> SelectExpired(
        IReadOnlyList<RunRecord> runs,
        RetentionSettings settings,
        DateTimeOffset now)
    {
        var cutoff = now.AddDays(-settings.Days);
        var expired = new List<string>();

        foreach (var group in runs.GroupBy(r => r.JobId))
        {
            var newestFirst = group.OrderByDescending(r => r.FinishedUtc).ToList();
            for (var i = 0; i < newestFirst.Count; i++)
            {
                var run = newestFirst[i];
                var beyondCount = i >= settings.RunsPerJob;
                var tooOld = run.FinishedUtc < cutoff;
                if (beyondCount || tooOld)
                {
                    expired.Add(run.Id);
                }
            }
        }
        return expired;
    }
}
