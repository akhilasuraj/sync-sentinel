using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public class ScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);

    private static Job Job(int interval = 15, bool enabled = true) => new()
    {
        Id = "j", Name = "J", Source = "s", Destination = "d",
        IntervalMinutes = interval, Enabled = enabled,
    };

    [Fact]
    public void A_never_run_enabled_job_is_due()
    {
        Assert.True(Schedule.IsDue(Job(), lastFinish: null, now: Now));
    }

    [Fact]
    public void Not_due_before_the_interval_elapses_since_last_finish()
    {
        var lastFinish = Now.AddMinutes(-10); // 10 < 15
        Assert.False(Schedule.IsDue(Job(interval: 15), lastFinish, Now));
    }

    [Fact]
    public void Due_once_the_interval_has_elapsed_since_last_finish()
    {
        var lastFinish = Now.AddMinutes(-15); // exactly due
        Assert.True(Schedule.IsDue(Job(interval: 15), lastFinish, Now));
    }

    [Fact]
    public void A_disabled_job_is_never_due_even_when_overdue()
    {
        var lastFinish = Now.AddMinutes(-120);
        Assert.False(Schedule.IsDue(Job(enabled: false), lastFinish, Now));
    }
}
