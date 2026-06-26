using Microsoft.AspNetCore.SignalR;

namespace SyncSentinel.Core;

/// <summary>
/// Starts a job run on a background task and broadcasts its lifecycle over the
/// <see cref="StatusHub"/>: <c>runStarted</c> (job name), <c>log</c> (each
/// streamed line), <c>runFinished</c> (status + exit code). Enforces one run at
/// a time (Phase 3 replaces this with the full scheduler + FIFO queue).
/// </summary>
public sealed class JobRunCoordinator(IHubContext<StatusHub> hub, RobocopyRunner runner)
{
    private int _running;

    /// <summary>Returns false if a run is already in progress.</summary>
    public bool TryStart(BackupJob job)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await hub.Clients.All.SendAsync("runStarted", job.Name);
                var result = await runner.RunAsync(
                    job,
                    line => hub.Clients.All.SendAsync("log", line));
                await hub.Clients.All.SendAsync("runFinished", result.Status.ToString(), result.ExitCode);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        });

        return true;
    }
}
