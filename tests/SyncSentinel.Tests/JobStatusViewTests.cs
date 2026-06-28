using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// The job status view: the per-job read-model the cards/dashboard render — a
/// job's last-run status, next-due time, and live queue state, projected from the
/// jobs, the history store's last run, and the run queue. Pure, so it's tested
/// directly (the endpoint that serves the feed is covered by JobStatusEndpointTests).
/// </summary>
public class JobStatusViewTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);

    private static Job Job(string id = "j", int interval = 15, bool enabled = true) => new()
    {
        Id = id, Name = "J", Source = "s", Destination = "d",
        IntervalMinutes = interval, Enabled = enabled,
    };

    private static RunRecord Run(string jobId, string status, DateTimeOffset finished) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        JobId = jobId, JobName = "J", Status = status,
        StartedUtc = finished.AddSeconds(-10), FinishedUtc = finished,
    };

    private static IReadOnlyList<JobStatusView> Project(
        IReadOnlyList<Job> jobs,
        Func<string, RunRecord?>? lastRun = null,
        string? running = null,
        IReadOnlyCollection<string>? pending = null) =>
        JobStatusView.Project(jobs, lastRun ?? (_ => null), running, pending ?? [], Now);

    [Fact]
    public void A_never_run_enabled_job_is_due_now_with_no_last_status()
    {
        var view = Assert.Single(Project([Job()]));

        Assert.Null(view.LastStatus);
        Assert.Equal(Now, view.NextDueUtc);
        Assert.Equal("Idle", view.State);
    }

    [Fact]
    public void A_job_with_a_last_run_reports_its_status_and_next_due_one_interval_later()
    {
        var finished = Now.AddMinutes(-5);
        var view = Assert.Single(Project([Job(interval: 15)], lastRun: _ => Run("j", "Warning", finished)));

        Assert.Equal("Warning", view.LastStatus);
        Assert.Equal(finished.AddMinutes(15), view.NextDueUtc);
    }

    [Fact]
    public void A_paused_job_has_no_next_due_even_with_a_last_run()
    {
        var view = Assert.Single(Project(
            [Job(enabled: false)], lastRun: _ => Run("j", "Success", Now.AddMinutes(-5))));

        Assert.Equal("Success", view.LastStatus); // last outcome still shown
        Assert.Null(view.NextDueUtc);
    }

    [Fact]
    public void The_job_in_the_run_slot_reports_running()
    {
        var view = Assert.Single(Project([Job("j")], running: "j"));

        Assert.Equal("Running", view.State);
    }

    [Fact]
    public void A_pending_job_reports_queued()
    {
        var view = Assert.Single(Project([Job("j")], pending: ["j"]));

        Assert.Equal("Queued", view.State);
    }

    [Fact]
    public void A_job_neither_running_nor_pending_reports_idle()
    {
        var view = Assert.Single(Project([Job("j")], running: "other", pending: ["another"]));

        Assert.Equal("Idle", view.State);
    }

    [Fact]
    public void Every_job_is_projected_in_order_each_resolved_independently()
    {
        var views = Project(
            [Job("a"), Job("b"), Job("c")],
            lastRun: id => id == "b" ? Run("b", "Error", Now.AddMinutes(-1)) : null,
            running: "a",
            pending: ["c"]);

        Assert.Equal(["a", "b", "c"], views.Select(v => v.JobId));
        Assert.Equal("Running", views[0].State);   // a is in the run slot
        Assert.Equal("Error", views[1].LastStatus); // b's own last run
        Assert.Equal("Queued", views[2].State);     // c is pending
    }
}
