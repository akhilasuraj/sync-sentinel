namespace SyncSentinel.Core;

/// <summary>
/// The pure due-policy for a job: the interval is anchored to the job's last
/// finish (not wall-clock), so a slow run never causes a pileup. A job that has
/// never run is due immediately (covers first run + catch-up after the machine
/// was off). Disabled (paused) jobs are never due.
/// </summary>
public static class Schedule
{
    public static bool IsDue(Job job, DateTimeOffset? lastFinish, DateTimeOffset now)
    {
        if (!job.Enabled)
        {
            return false;
        }
        if (lastFinish is null)
        {
            return true; // never run -> first run / catch-up
        }
        return now >= lastFinish.Value.AddMinutes(job.IntervalMinutes);
    }

    public static DateTimeOffset NextDue(Job job, DateTimeOffset? lastFinish) =>
        (lastFinish ?? DateTimeOffset.MinValue).AddMinutes(job.IntervalMinutes);
}
