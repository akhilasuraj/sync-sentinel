namespace SyncSentinel.Core;

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

    public string? Running { get { lock (_gate) { return _running; } } }

    public IReadOnlyList<string> Pending { get { lock (_gate) { return _pending.ToList(); } } }

    /// <summary>Enqueue a job; returns false if it is already pending or running.</summary>
    public bool Enqueue(string jobId, bool front = false)
    {
        lock (_gate)
        {
            if (_running == jobId || _pending.Contains(jobId))
            {
                return false;
            }
            if (front)
            {
                _pending.AddFirst(jobId);
            }
            else
            {
                _pending.AddLast(jobId);
            }
            return true;
        }
    }

    /// <summary>Take the next pending job into the running slot, or null if empty/busy.</summary>
    public string? Dequeue()
    {
        lock (_gate)
        {
            if (_running is not null || _pending.Count == 0)
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
}
