using Microsoft.Extensions.Hosting;

namespace SyncSentinel.Core;

/// <summary>
/// Drains the run queue continuously (pumps immediately on start, then polls so
/// a freshly-enqueued "run now" starts within the poll interval). Registered in
/// every host — including tests — so enqueued jobs actually execute.
/// </summary>
public sealed class QueuePumpService(Scheduler scheduler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        try
        {
            do
            {
                await scheduler.PumpAsync();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // host shutting down — expected
        }
    }
}

/// <summary>
/// Periodically enqueues due jobs (and once immediately on start — login/wake
/// catch-up). Registered only by the shell, not by tests, so unit/integration
/// tests never get surprise scheduled runs.
/// </summary>
public sealed class SchedulerTickService(Scheduler scheduler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            do
            {
                scheduler.Tick();
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // host shutting down — expected
        }
    }
}
