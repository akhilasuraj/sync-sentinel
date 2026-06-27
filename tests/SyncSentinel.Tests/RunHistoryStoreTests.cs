using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class RunHistoryStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ss-hist-" + Guid.NewGuid().ToString("N"));
    private readonly RunHistoryStore _store;

    public RunHistoryStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new RunHistoryStore(Path.Combine(_dir, "history.db"));
    }

    public void Dispose()
    {
        _store.Dispose(); // close the connection so the file can be deleted
        Directory.Delete(_dir, recursive: true);
    }

    private static RunRecord Run(string id, string jobId, DateTimeOffset finished) => new()
    {
        Id = id,
        JobId = jobId,
        JobName = "Job " + jobId,
        Status = "Success",
        StartedUtc = finished.AddSeconds(-5),
        FinishedUtc = finished,
        FilesCopied = 3,
        FilesExtra = 1,
        ExitCode = 1,
        LogPath = @"C:\logs\x.log",
    };

    [Fact]
    public void Add_then_ListByJob_round_trips_the_record()
    {
        var when = new DateTimeOffset(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
        _store.Add(Run("r1", "j", when));

        var list = _store.ListByJob("j");

        var r = Assert.Single(list);
        Assert.Equal("r1", r.Id);
        Assert.Equal("Success", r.Status);
        Assert.Equal(3, r.FilesCopied);
        Assert.Equal(1, r.FilesExtra);
        Assert.Equal(when, r.FinishedUtc);
        Assert.Equal(@"C:\logs\x.log", r.LogPath);
    }

    [Fact]
    public void ListByJob_returns_newest_first_and_respects_the_limit()
    {
        var t0 = new DateTimeOffset(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
        _store.Add(Run("old", "j", t0));
        _store.Add(Run("mid", "j", t0.AddMinutes(1)));
        _store.Add(Run("new", "j", t0.AddMinutes(2)));

        var top2 = _store.ListByJob("j", limit: 2);

        Assert.Equal(["new", "mid"], top2.Select(r => r.Id));
    }

    [Fact]
    public void Recent_returns_the_newest_runs_across_all_jobs_capped_at_the_limit()
    {
        var t0 = new DateTimeOffset(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
        _store.Add(Run("a", "j1", t0));
        _store.Add(Run("b", "j2", t0.AddMinutes(1)));
        _store.Add(Run("c", "j1", t0.AddMinutes(2)));

        var recent = _store.Recent(2);

        Assert.Equal(["c", "b"], recent.Select(r => r.Id));
    }

    [Fact]
    public void ListByJob_only_returns_runs_for_that_job()
    {
        var t0 = new DateTimeOffset(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
        _store.Add(Run("a", "j1", t0));
        _store.Add(Run("b", "j2", t0));

        Assert.Equal(["a"], _store.ListByJob("j1").Select(r => r.Id));
    }

    [Fact]
    public void Get_returns_the_run_by_id_or_null_when_missing()
    {
        var t0 = new DateTimeOffset(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
        _store.Add(Run("a", "j", t0));

        Assert.Equal("a", _store.Get("a")?.Id);
        Assert.Null(_store.Get("missing"));
    }

    [Fact]
    public void Delete_removes_the_given_runs()
    {
        var t0 = new DateTimeOffset(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);
        _store.Add(Run("a", "j", t0));
        _store.Add(Run("b", "j", t0.AddMinutes(1)));

        _store.Delete(["a"]);

        Assert.Equal(["b"], _store.ListByJob("j").Select(r => r.Id));
    }
}
