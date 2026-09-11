namespace SyncSentinel.Core;

public enum RunQueueEnqueueResult
{
    Enqueued,
    Duplicate,
    UpdateReserved,
}

/// <summary>
/// The single global run queue. Holds pending job ids in FIFO order with at most
/// one job running at a time (maxConcurrent = 1). Enqueue de-duplicates — a job
/// already pending or running is never queued twice (no self-overlap). "Run now"
/// enqueues at the front to jump ahead. Thread-safe; the scheduler pump and the
/// API both touch it.
/// </summary>
public sealed class RunQueue
{
    private readonly object _gate = new();
    private readonly LinkedList<string> _pending = new();
    private string? _running;
    private bool _reservedForUpdate;

    public string? Running { get { lock (_gate) { return _running; } } }

    public IReadOnlyList<string> Pending { get { lock (_gate) { return _pending.ToList(); } } }

    /// <summary>
    /// Enqueue a job; returns false when it is already present or installer handoff reserved the queue.
    /// </summary>
    public bool Enqueue(string jobId, bool front = false) =>
        TryEnqueue(jobId, front) == RunQueueEnqueueResult.Enqueued;

    public RunQueueEnqueueResult TryEnqueue(string jobId, bool front = false)
    {
        lock (_gate)
        {
            if (_reservedForUpdate)
            {
                return RunQueueEnqueueResult.UpdateReserved;
            }
            if (_running == jobId || _pending.Contains(jobId))
            {
                return RunQueueEnqueueResult.Duplicate;
            }
            if (front)
            {
                _pending.AddFirst(jobId);
            }
            else
            {
                _pending.AddLast(jobId);
            }
            return RunQueueEnqueueResult.Enqueued;
        }
    }

    /// <summary>Take the next pending job into the running slot, or null if empty/busy.</summary>
    public string? Dequeue()
    {
        lock (_gate)
        {
            if (_reservedForUpdate || _running is not null || _pending.Count == 0)
            {
                return null;
            }
            var next = _pending.First!.Value;
            _pending.RemoveFirst();
            _running = next;
            return next;
        }
    }

    /// <summary>Mark the running job complete, freeing the slot.</summary>
    public void Complete(string jobId)
    {
        lock (_gate)
        {
            if (_running == jobId)
            {
                _running = null;
            }
        }
    }

    /// <summary>
    /// Atomically proves the queue is idle and prevents new work from entering
    /// while the updater hands control to the installer.
    /// </summary>
    public bool TryReserveForUpdate()
    {
        lock (_gate)
        {
            if (_reservedForUpdate || _running is not null || _pending.Count > 0)
            {
                return false;
            }
            _reservedForUpdate = true;
            return true;
        }
    }

    public void ReleaseUpdateReservation()
    {
        lock (_gate)
        {
            _reservedForUpdate = false;
        }
    }
}
