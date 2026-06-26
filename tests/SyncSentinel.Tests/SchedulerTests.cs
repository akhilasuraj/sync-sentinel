using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class SchedulerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ss-sched-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _ran = [];
    private readonly ConfigService _cfg;
    private readonly Scheduler _sched;
    private DateTimeOffset _now = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);

    public SchedulerTests()
    {
        Directory.CreateDirectory(_dir);
        _cfg = new ConfigService(new ConfigStore(_dir));
        _sched = new Scheduler(_cfg, new RunQueue(),
            job => { lock (_ran) { _ran.Add(job.Name); } return Task.CompletedTask; },
            () => _now);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private Job AddJob(string name, int interval = 15, bool enabled = true) =>
        _cfg.AddJob(new Job { Name = name, Source = "s", Destination = "d", IntervalMinutes = interval, Enabled = enabled });

    private int RunsOf(string name) => _ran.Count(n => n == name);

    [Fact]
    public void RunNow_returns_false_for_an_unknown_job()
    {
        Assert.False(_sched.RunNow("nope"));
    }

    [Fact]
    public async Task RunNow_then_pump_executes_the_job()
    {
        var job = AddJob("PEMS");

        Assert.True(_sched.RunNow(job.Id));
        await _sched.PumpAsync();

        Assert.Equal(1, RunsOf("PEMS"));
    }

    [Fact]
    public async Task Tick_schedules_a_due_job_and_pump_runs_it()
    {
        AddJob("PEMS");

        _sched.Tick();
        await _sched.PumpAsync();

        Assert.Equal(1, RunsOf("PEMS"));
    }

    [Fact]
    public async Task A_disabled_job_is_never_scheduled()
    {
        AddJob("Paused", enabled: false);

        _sched.Tick();
        await _sched.PumpAsync();

        Assert.Equal(0, RunsOf("Paused"));
    }

    [Fact]
    public async Task A_job_is_not_rerun_until_its_interval_elapses_since_last_finish()
    {
        AddJob("PEMS", interval: 15);

        _sched.Tick();
        await _sched.PumpAsync(); // first run (never-run -> due)
        _sched.Tick();
        await _sched.PumpAsync(); // still 12:00 -> not due yet
        Assert.Equal(1, RunsOf("PEMS"));

        _now = _now.AddMinutes(16); // interval elapsed since last finish
        _sched.Tick();
        await _sched.PumpAsync();
        Assert.Equal(2, RunsOf("PEMS"));
    }

    [Fact]
    public async Task Ticking_twice_before_a_pump_does_not_double_run()
    {
        AddJob("PEMS");

        _sched.Tick();
        _sched.Tick(); // dedup — already pending
        await _sched.PumpAsync();

        Assert.Equal(1, RunsOf("PEMS"));
    }
}
