using System.Net.Http.Headers;
using System.Text.Json;

namespace SyncSentinel.Core;

public sealed record UpdateRelease(string Version, string ReleaseUrl);

public enum PortableUpdateChoice
{
    RemindLater,
    SkipVersion,
    ViewRelease,
}

public sealed record PortableUpdateState
{
    public DateTimeOffset? LastCheckedUtc { get; init; }
    public string? SkippedVersion { get; init; }
}

public interface IUpdateReleaseSource
{
    Task<UpdateRelease> GetLatestAsync(CancellationToken cancellationToken = default);
}

public interface IPortableUpdateStateStore
{
    PortableUpdateState Load();
    void Save(PortableUpdateState state);
}

public interface IPortableUpdateInteraction
{
    Task<PortableUpdateChoice> PromptAsync(UpdateRelease release, CancellationToken cancellationToken = default);
}

public sealed class GitHubUpdateReleaseSource(HttpClient httpClient) : IUpdateReleaseSource
{
    public async Task<UpdateRelease> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/repos/akhilasuraj/sync-sentinel/releases/latest");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SyncSentinel", "updater"));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var tag = body.RootElement.GetProperty("tag_name").GetString()
            ?? throw new JsonException("The latest release has no tag_name.");
        var releaseUrl = body.RootElement.GetProperty("html_url").GetString()
            ?? throw new JsonException("The latest release has no html_url.");
        return new UpdateRelease(tag.TrimStart('v', 'V'), releaseUrl);
    }
}

public sealed class JsonPortableUpdateStateStore
    : IPortableUpdateStateStore
{
    private readonly string _path;

    public JsonPortableUpdateStateStore(StoragePaths paths) =>
        _path = Path.Combine(paths.Root, "update-state.json");

    public PortableUpdateState Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<PortableUpdateState>(File.ReadAllText(_path)) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }

    public void Save(PortableUpdateState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(state));
    }
}

public sealed class PortableUpdateService : IAppUpdateService
{
    private readonly string _currentVersion;
    private readonly IUpdateReleaseSource _releases;
    private readonly IPortableUpdateStateStore _stateStore;
    private readonly IPortableUpdateInteraction _interaction;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _checkGate = new(1, 1);

    public PortableUpdateService(
        string currentVersion,
        IUpdateReleaseSource releases,
        IPortableUpdateStateStore stateStore,
        IPortableUpdateInteraction interaction,
        TimeProvider? timeProvider = null)
    {
        _currentVersion = currentVersion;
        _releases = releases;
        _stateStore = stateStore;
        _interaction = interaction;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Status = AppUpdateStatus.Idle(Distribution);
    }

    public AppDistribution Distribution => AppDistribution.Portable;
    public bool Available => true;
    public AppUpdateStatus Status { get; private set; }

    public async Task<AppUpdateStatus> CheckAsync(
        UpdateCheckMode mode,
        CancellationToken cancellationToken = default)
    {
        await _checkGate.WaitAsync(cancellationToken);
        try
        {
            var manual = mode == UpdateCheckMode.UserRequested;
            var state = _stateStore.Load();
            var now = _timeProvider.GetUtcNow();
            if (!PortableUpdatePolicy.ShouldCheck(state.LastCheckedUtc, now, manual))
            {
                return Status = Status with
                {
                    State = AppUpdateCheckState.Idle,
                    Message = "The next automatic update check is not due yet.",
                };
            }

            Status = Status with { State = AppUpdateCheckState.Checking, Message = "Checking for updates…" };
            var release = await _releases.GetLatestAsync(cancellationToken);
            state = state with { LastCheckedUtc = now };
            _stateStore.Save(state);

            if (!PortableUpdatePolicy.IsNewer(release.Version, _currentVersion))
            {
                return Status = new(Distribution, AppUpdateCheckState.UpToDate, null,
                    "SyncSentinel is up to date.", null);
            }

            Status = new(Distribution, AppUpdateCheckState.UpdateAvailable, release.Version,
                $"SyncSentinel {release.Version} is available. Portable copies are updated manually.",
                release.ReleaseUrl);

            if (PortableUpdatePolicy.ShouldPrompt(release.Version, state.SkippedVersion, manual))
            {
                var choice = await _interaction.PromptAsync(release, cancellationToken);
                if (choice == PortableUpdateChoice.SkipVersion)
                {
                    _stateStore.Save(state with { SkippedVersion = release.Version });
                }
            }

            return Status;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Status = Status with
            {
                State = AppUpdateCheckState.Error,
                Message = $"Update check failed: {exception.Message}",
            };
        }
        finally
        {
            _checkGate.Release();
        }
    }
}
