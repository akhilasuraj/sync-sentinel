using System.Text.Json.Serialization;
using NuGet.Versioning;

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

[JsonConverter(typeof(JsonStringEnumConverter<UpdateCheckMode>))]
public enum UpdateCheckMode
{
    Automatic,
    UserRequested,
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
    Task<AppUpdateStatus> CheckAsync(UpdateCheckMode mode, CancellationToken cancellationToken = default);
}

public sealed class NoOpAppUpdateService : IAppUpdateService
{
    public AppDistribution Distribution => AppDistribution.Portable;
    public bool Available => false;
    public AppUpdateStatus Status => AppUpdateStatus.Idle(Distribution) with
    {
        Message = "Update checks are unavailable outside the desktop app.",
    };

    public Task<AppUpdateStatus> CheckAsync(UpdateCheckMode mode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Status);
}

public sealed class UpdateInstallGuard(RunQueue queue)
{
    public bool TryReserve(out string reason)
    {
        if (!queue.TryReserveForUpdate())
        {
            reason = "Finish or clear active backup runs before installing the update.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void Release() => queue.ReleaseUpdateReservation();
}

public static class PortableUpdatePolicy
{
    public static readonly TimeSpan CheckCooldown = TimeSpan.FromHours(24);

    public static bool ShouldCheck(DateTimeOffset? lastCheckedUtc, DateTimeOffset now, UpdateCheckMode mode) =>
        mode == UpdateCheckMode.UserRequested || lastCheckedUtc is null || now - lastCheckedUtc >= CheckCooldown;

    public static bool ShouldPrompt(string version, string? skippedVersion, UpdateCheckMode mode) =>
        mode == UpdateCheckMode.UserRequested ||
        !string.Equals(version, skippedVersion, StringComparison.OrdinalIgnoreCase);

    public static bool IsNewer(string candidate, string current) =>
        NuGetVersion.TryParse(candidate.TrimStart('v', 'V'), out var candidateVersion) &&
        NuGetVersion.TryParse(current.TrimStart('v', 'V'), out var currentVersion) &&
        candidateVersion > currentVersion;
}

public static class UpdateStartup
{
    public static Task<AppUpdateStatus?> CheckAsync(
        GlobalSettings settings,
        IAppUpdateService updates,
        CancellationToken cancellationToken = default) =>
        settings.AutomaticUpdateChecks
            ? CheckEnabledAsync(updates, cancellationToken)
            : Task.FromResult<AppUpdateStatus?>(null);

    private static async Task<AppUpdateStatus?> CheckEnabledAsync(
        IAppUpdateService updates,
        CancellationToken cancellationToken) =>
        await updates.CheckAsync(UpdateCheckMode.Automatic, cancellationToken);
}
