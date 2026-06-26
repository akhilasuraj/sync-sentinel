using Microsoft.AspNetCore.SignalR;

namespace SyncSentinel.Core;

/// <summary>
/// Executes a single resolved <see cref="BackupJob"/> end-to-end: broadcasts its
/// lifecycle over the <see cref="StatusHub"/> (<c>runStarted</c> / <c>log</c> /
/// <c>runFinished</c>), captures the output, and records the run to history via
/// <see cref="RunRecorder"/>. Serialization/ordering are owned by the
/// <see cref="Scheduler"/>/<see cref="RunQueue"/>.
/// </summary>
public sealed class JobRunCoordinator(IHubContext<StatusHub> hub, RobocopyRunner runner, RunRecorder recorder)
{
    public async Task RunAsync(BackupJob job)
    {
        var started = DateTimeOffset.UtcNow;
        var lines = new List<string>();

        await hub.Clients.All.SendAsync("runStarted", job.Name);
        var result = await runner.RunAsync(job, line =>
        {
            lock (lines)
            {
                lines.Add(line);
            }
            _ = hub.Clients.All.SendAsync("log", line);
        });
        var finished = DateTimeOffset.UtcNow;
        // Record first so "runFinished" means "fully recorded" — a consumer that
        // reacts to it (the UI, a test) can immediately read the run from history.
        recorder.Record(job.JobId, job.Name, started, finished, result.Status, result.ExitCode, lines);
        await hub.Clients.All.SendAsync("runFinished", result.Status.ToString(), result.ExitCode);
    }
}
