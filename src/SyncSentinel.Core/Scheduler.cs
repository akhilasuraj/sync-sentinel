using System.Collections.Concurrent;

namespace SyncSentinel.Core;

/// <summary>
/// Drives backups: <see cref="Tick"/> enqueues every due job (per <see
/// cref="Schedule"/>, anchored to its last finish), <see cref="RunNow"/> jumps a
/// job to the front, and <see cref="PumpAsync"/> drains the <see cref="RunQueue"/>
/// one job at a time through the executor, recording each finish. A clock and the
/// executor are injectable so the scheduling logic is deterministically testable.
/// </summary>
public sealed class Scheduler
{
    private readonly ConfigService _config;
    private readonly RunQueue _queue;
    private readonly Func<BackupJob, Task> _executor;
    private readonly Func<DateTimeOffset> _now;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastFinish = new();
    private readonly SemaphoreSlim _pumpLock = new(1, 1);

    // DI: execute via the coordinator, real wall clock.
    public Scheduler(ConfigService config, RunQueue queue, JobRunCoordinator coordinator)
        : this(config, queue, coordinator.RunAsync, () => DateTimeOffset.UtcNow)
    {
    }

    // Test seam: fake executor + controllable clock.
    public Scheduler(ConfigService config, RunQueue queue, Func<BackupJob, Task> executor, Func<DateTimeOffset> now)
    {
        _config = config;
        _queue = queue;
        _executor = executor;
        _now = now;
    }

    /// <summary>Queue a job to run now (jumps the queue). False if the job is unknown.</summary>
    public bool RunNow(string jobId)
    {
        if (_config.ResolveJob(jobId) is null)
        {
            return false;
        }
        _queue.Enqueue(jobId, front: true);
        return true;
    }

    /// <summary>Enqueue every job that is currently due.</summary>
    public void Tick()
    {
        var now = _now();
        foreach (var job in _config.Current.Jobs)
        {
            var last = _lastFinish.TryGetValue(job.Id, out var lf) ? lf : (DateTimeOffset?)null;
            if (Schedule.IsDue(job, last, now))
            {
                _queue.Enqueue(job.Id); // back of the queue; dedupes if pending/running
            }
        }
    }

    /// <summary>Drain the queue, running each job to completion in turn.</summary>
    public async Task PumpAsync()
    {
        // Only one drainer at a time; concurrent callers bail (the active drainer
        // loops until the queue is empty, so it picks up anything just enqueued).
        if (!await _pumpLock.WaitAsync(0))
        {
            return;
        }
        try
        {
            while (_queue.Dequeue() is { } id)
            {
                try
                {
                    var resolved = _config.ResolveJob(id);
                    if (resolved is not null)
                    {
                        await _executor(resolved);
                    }
                }
                finally
                {
                    _lastFinish[id] = _now();
                    _queue.Complete(id);
                }
            }
        }
        finally
        {
            _pumpLock.Release();
        }
    }
}
