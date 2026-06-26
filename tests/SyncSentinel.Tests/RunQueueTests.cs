using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public class RunQueueTests
{
    [Fact]
    public void Dequeue_returns_the_enqueued_job_and_marks_it_running()
    {
        var q = new RunQueue();
        Assert.True(q.Enqueue("a"));

        Assert.Equal("a", q.Dequeue());
        Assert.Equal("a", q.Running);
    }

    [Fact]
    public void Dequeue_follows_FIFO_order()
    {
        var q = new RunQueue();
        q.Enqueue("a");
        q.Enqueue("b");

        Assert.Equal("a", q.Dequeue());
        q.Complete("a");
        Assert.Equal("b", q.Dequeue());
    }

    [Fact]
    public void Only_one_job_runs_at_a_time()
    {
        var q = new RunQueue();
        q.Enqueue("a");
        q.Enqueue("b");

        Assert.Equal("a", q.Dequeue());
        Assert.Null(q.Dequeue()); // busy — b waits
        q.Complete("a");
        Assert.Equal("b", q.Dequeue());
    }

    [Fact]
    public void Enqueue_deduplicates_a_job_already_pending()
    {
        var q = new RunQueue();
        Assert.True(q.Enqueue("a"));
        Assert.False(q.Enqueue("a"));

        Assert.Equal(["a"], q.Pending);
    }

    [Fact]
    public void Enqueue_refuses_a_job_that_is_currently_running()
    {
        var q = new RunQueue();
        q.Enqueue("a");
        q.Dequeue(); // a now running

        Assert.False(q.Enqueue("a")); // no self-overlap
    }

    [Fact]
    public void Front_enqueue_jumps_ahead_of_pending_jobs()
    {
        var q = new RunQueue();
        q.Enqueue("a");
        q.Enqueue("b", front: true);

        Assert.Equal("b", q.Dequeue());
    }
}
