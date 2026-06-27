using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public class RunStatsTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Since = Now.AddDays(-7);

    private static RunRecord Run(string status, DateTimeOffset finished, int copied = 0) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        JobId = "j",
        JobName = "J",
        Status = status,
        StartedUtc = finished.AddSeconds(-1),
        FinishedUtc = finished,
        FilesCopied = copied,
    };

    [Fact]
    public void Counts_runs_and_sums_files_copied_in_the_window()
    {
        var stats = RunStats.Summarize([Run("Success", Now.AddDays(-1), 10), Run("Success", Now.AddDays(-2), 5)], Since);

        Assert.Equal(2, stats.Runs);
        Assert.Equal(15, stats.FilesCopied);
        Assert.Equal(0, stats.Failures);
    }

    [Fact]
    public void Excludes_runs_before_the_cutoff()
    {
        var stats = RunStats.Summarize([Run("Success", Now.AddDays(-1), 10), Run("Success", Now.AddDays(-8), 99)], Since);

        Assert.Equal(1, stats.Runs);
        Assert.Equal(10, stats.FilesCopied);
    }

    [Fact]
    public void Counts_Error_and_Skipped_as_failures()
    {
        var stats = RunStats.Summarize(
            [Run("Success", Now.AddDays(-1)), Run("Warning", Now.AddDays(-1)), Run("Error", Now.AddDays(-1)), Run("Skipped", Now.AddDays(-1))],
            Since)
        ;

        Assert.Equal(4, stats.Runs);
        Assert.Equal(2, stats.Failures); // Error + Skipped; Warning is not a failure
    }
}
