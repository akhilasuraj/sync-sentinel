using System.Text.Json.Serialization;

namespace SyncSentinel.Core;

[JsonConverter(typeof(JsonStringEnumConverter<AppUpdateCheckState>))]
public enum AppUpdateCheckState
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Error,
}

public sealed record AppUpdateStatus(
    AppDistribution Distribution,
    AppUpdateCheckState State,
    string? Version,
    string Message,
    string? ReleaseUrl)
{
    public static AppUpdateStatus Idle(AppDistribution distribution) =>
        new(distribution, AppUpdateCheckState.Idle, null, "Updates have not been checked yet.", null);
}

public interface IAppUpdateService
{
    AppDistribution Distribution { get; }
    bool Available { get; }
    AppUpdateStatus Status { get; }
    Task<AppUpdateStatus> CheckAsync(bool manual, CancellationToken cancellationToken = default);
}

public sealed class NoOpAppUpdateService : IAppUpdateService
{
    public AppDistribution Distribution => AppDistribution.Portable;
    public bool Available => false;
    public AppUpdateStatus Status => AppUpdateStatus.Idle(Distribution) with
    {
        Message = "Update checks are unavailable outside the desktop app.",
    };

    public Task<AppUpdateStatus> CheckAsync(bool manual, CancellationToken cancellationToken = default) =>
        Task.FromResult(Status);
}

public sealed class UpdateInstallGuard(RunQueue queue)
{
    public bool CanInstall(out string reason)
    {
        if (queue.Running is not null || queue.Pending.Count > 0)
        {
            reason = "Finish or clear active backup runs before installing the update.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
