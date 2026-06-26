using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public class RetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);

    private static RunRecord Run(string id, string jobId, int ageDays) => new()
    {
        Id = id,
        JobId = jobId,
        JobName = jobId,
        Status = "Success",
        StartedUtc = Now.AddDays(-ageDays),
        FinishedUtc = Now.AddDays(-ageDays),
    };

    [Fact]
    public void Keeps_runs_within_both_caps()
    {
        var runs = new[] { Run("a", "j", 0), Run("b", "j", 1) };

        var expired = Retention.SelectExpired(runs, new RetentionSettings { RunsPerJob = 100, Days = 30 }, Now);

        Assert.Empty(expired);
    }

    [Fact]
    public void Prunes_runs_beyond_the_per_job_count_cap()
    {
        // 3 runs, cap 2 -> the oldest (c) is expired.
        var runs = new[] { Run("a", "j", 0), Run("b", "j", 1), Run("c", "j", 2) };

        var expired = Retention.SelectExpired(runs, new RetentionSettings { RunsPerJob = 2, Days = 30 }, Now);

        Assert.Equal(["c"], expired);
    }

    [Fact]
    public void Prunes_runs_older_than_the_day_cap_even_within_the_count()
    {
        var runs = new[] { Run("recent", "j", 1), Run("old", "j", 40) };

        var expired = Retention.SelectExpired(runs, new RetentionSettings { RunsPerJob = 100, Days = 30 }, Now);

        Assert.Equal(["old"], expired);
    }

    [Fact]
    public void Counts_the_per_job_cap_independently_per_job()
    {
        // Cap 1 per job: each job keeps its newest, prunes its older.
        var runs = new[]
        {
            Run("j1-new", "j1", 0), Run("j1-old", "j1", 1),
            Run("j2-new", "j2", 0), Run("j2-old", "j2", 1),
        };

        var expired = Retention.SelectExpired(runs, new RetentionSettings { RunsPerJob = 1, Days = 30 }, Now);

        Assert.Equal(2, expired.Count);
        Assert.Contains("j1-old", expired);
        Assert.Contains("j2-old", expired);
    }
}
