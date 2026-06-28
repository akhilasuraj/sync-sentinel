namespace SyncSentinel.Core;

/// <summary>
/// The per-job read-model the dashboard and job cards render (the "job status
/// view" — see CONTEXT.md): a job's last-run status, its next-due time, and its
/// live queue state, projected from the jobs, a last-run lookup, and the run
/// queue's running/pending slots. Pure over its inputs so the projection is the
/// test surface; the feed endpoint just serializes <see cref="Project"/>.
/// </summary>
public sealed record JobStatusView(string JobId, string? LastStatus, DateTimeOffset? NextDueUtc, string State)
{
    public static IReadOnlyList<JobStatusView> Project(
        IReadOnlyList<Job> jobs,
        Func<string, RunRecord?> lastRun,
        string? running,
        IReadOnlyCollection<string> pending,
        DateTimeOffset now)
    {
        var pendingSet = pending as ISet<string> ?? pending.ToHashSet();
        return jobs.Select(job =>
        {
            var last = lastRun(job.Id);
            var nextDue = !job.Enabled ? (DateTimeOffset?)null
                : last is null ? now
                : Schedule.NextDue(job, last.FinishedUtc);
            var state = running == job.Id ? "Running"
                : pendingSet.Contains(job.Id) ? "Queued"
                : "Idle";
            return new JobStatusView(job.Id, last?.Status, nextDue, state);
        }).ToList();
    }
}
