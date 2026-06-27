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
        // Skip (don't invoke robocopy) when a precondition fails — e.g. the source
        // went missing since the job was scheduled. Recorded as a "Skipped" run so
        // it's visible in history instead of producing a confusing robocopy error.
        var reason = RunPreconditions.Check(job.Source, job.Destination);
        if (reason is not null)
        {
            recorder.RecordSkipped(job.JobId, job.Name, reason, DateTimeOffset.UtcNow);
            await hub.Clients.All.SendAsync("runFinished", "Skipped", -1);
            return;
        }

        var started = DateTimeOffset.UtcNow;
        var lines = new List<string>();

        await hub.Clients.All.SendAsync("runStarted", job.JobId, job.Name);
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
