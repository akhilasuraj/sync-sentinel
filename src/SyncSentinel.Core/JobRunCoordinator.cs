using Microsoft.AspNetCore.SignalR;

namespace SyncSentinel.Core;

/// <summary>
/// Executes a single resolved <see cref="BackupJob"/> end-to-end and broadcasts
/// its lifecycle over the <see cref="StatusHub"/>: <c>runStarted</c> (job name),
/// <c>log</c> (each streamed line), <c>runFinished</c> (status + exit code).
/// Serialization and ordering are owned by the <see cref="Scheduler"/>/<see
/// cref="RunQueue"/>, so this just runs the one job it is handed.
/// </summary>
public sealed class JobRunCoordinator(IHubContext<StatusHub> hub, RobocopyRunner runner)
{
    public async Task RunAsync(BackupJob job)
    {
        await hub.Clients.All.SendAsync("runStarted", job.Name);
        var result = await runner.RunAsync(job, line => hub.Clients.All.SendAsync("log", line));
        await hub.Clients.All.SendAsync("runFinished", result.Status.ToString(), result.ExitCode);
    }
}
