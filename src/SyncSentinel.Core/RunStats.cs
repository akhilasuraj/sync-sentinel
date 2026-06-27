namespace SyncSentinel.Core;

/// <summary>
/// Aggregate run metrics over a time window, for the dashboard's summary widget:
/// how many runs, how many files copied, and how many failed (Error or Skipped).
/// Pure over a run list so it's unit-testable without the store.
/// </summary>
public sealed record RunStats(int Runs, int FilesCopied, int Failures)
{
    public static RunStats Summarize(IReadOnlyList<RunRecord> runs, DateTimeOffset since)
    {
        var window = runs.Where(r => r.FinishedUtc >= since).ToList();
        var failures = window.Count(r => r.Status is "Error" or "Skipped");
        return new RunStats(window.Count, window.Sum(r => r.FilesCopied), failures);
    }
}
