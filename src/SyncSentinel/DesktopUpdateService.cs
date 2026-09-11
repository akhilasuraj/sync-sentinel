using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using SyncSentinel.Core;

namespace SyncSentinel;

/// <summary>
/// Distribution-aware desktop updater. Installed builds use NetSparkle's signed
/// appcast and native Update / Skip / Remind Later UI. Portable builds only
/// notify and open the GitHub release page; they never replace the running exe.
/// </summary>
internal sealed class DesktopUpdateService : IAppUpdateService, IDisposable
{
    private const string AppcastUrl =
        "https://github.com/akhilasuraj/sync-sentinel/releases/latest/download/appcast.xml";
    private const string LatestReleaseApi =
        "https://api.github.com/repos/akhilasuraj/sync-sentinel/releases/latest";
    private const string PublicKey = "nEDaY7ezIENToBpyirIB7a/EK+LTwW8qBUvF7WOjxzM=";
    private static readonly TimeSpan PortableCheckCooldown = TimeSpan.FromHours(24);

    private readonly string _currentVersion;
    private readonly string _portableStatePath;
    private readonly UpdateInstallGuard _installGuard;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private MainForm? _form;
    private SparkleUpdater? _sparkle;
    private System.Windows.Forms.Timer? _retryTimer;

    public DesktopUpdateService(
        AppDistribution distribution,
        string currentVersion,
        StoragePaths paths,
        RunQueue queue)
    {
        Distribution = distribution;
        _currentVersion = currentVersion;
        _portableStatePath = Path.Combine(paths.Root, "update-state.json");
        _installGuard = new UpdateInstallGuard(queue);
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SyncSentinel", currentVersion));
        Status = AppUpdateStatus.Idle(distribution);
    }

    public AppDistribution Distribution { get; }
    public bool Available => _form is not null;
    public AppUpdateStatus Status { get; private set; }

    /// <summary>Called once on the WinForms thread after the main form exists.</summary>
    public void SetForm(MainForm form)
    {
        _form = form;
        if (Distribution != AppDistribution.Installed)
        {
            return;
        }

        _sparkle = new SparkleUpdater(
            AppcastUrl,
            new Ed25519Checker(SecurityMode.Strict, PublicKey))
        {
            UIFactory = new NetSparkleUpdater.UI.WinForms.UIFactory(form.Icon),
            RelaunchAfterUpdate = true,
            CustomInstallerArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
        };
        _retryTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _retryTimer.Tick += async (_, _) =>
        {
            if (!_installGuard.CanInstall(out _))
            {
                return;
            }

            _retryTimer.Stop();
            await CheckAsync(manual: false);
        };
        _sparkle.PreparingToExit += (_, eventArgs) =>
        {
            if (_installGuard.CanInstall(out var reason))
            {
                return;
            }

            eventArgs.Cancel = true;
            _retryTimer.Start();
            ShowMessage(reason, "Update waiting", MessageBoxIcon.Information);
            Status = Status with { Message = reason };
        };
        _sparkle.CloseApplication += () => form.BeginInvoke(form.ExitApplication);
    }

    public async Task<AppUpdateStatus> CheckAsync(bool manual, CancellationToken cancellationToken = default)
    {
        if (_form is null)
        {
            return Status = Status with { State = AppUpdateCheckState.Error, Message = "The desktop window is not ready yet." };
        }

        await _checkGate.WaitAsync(cancellationToken);
        try
        {
            Status = Status with { State = AppUpdateCheckState.Checking, Message = "Checking for updates…" };
            return Distribution == AppDistribution.Installed
                ? await CheckInstalledAsync(manual)
                : await CheckPortableAsync(manual, cancellationToken);
        }
        catch (Exception exception)
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

    private async Task<AppUpdateStatus> CheckInstalledAsync(bool manual)
    {
        var info = manual
            ? await _sparkle!.CheckForUpdatesAtUserRequest(ignoreSkippedVersions: true)
            : await _sparkle!.CheckForUpdatesQuietly();

        var item = info.Updates.FirstOrDefault();
        Status = info.Status switch
        {
            UpdateStatus.UpdateAvailable => new(
                Distribution, AppUpdateCheckState.UpdateAvailable, item?.Version,
                $"SyncSentinel {item?.Version ?? "update"} is available.", item?.ReleaseNotesLink),
            UpdateStatus.UserSkipped => new(
                Distribution, AppUpdateCheckState.UpdateAvailable, item?.Version,
                $"SyncSentinel {item?.Version ?? "update"} was skipped.", item?.ReleaseNotesLink),
            UpdateStatus.UpdateNotAvailable => new(
                Distribution, AppUpdateCheckState.UpToDate, null, "SyncSentinel is up to date.", null),
            _ => new(
                Distribution, AppUpdateCheckState.Error, null, "The update feed could not be reached.", null),
        };

        if (!manual && info.Status == UpdateStatus.UpdateAvailable)
        {
            _sparkle.ShowUpdateNeededUI(info.Updates);
        }

        return Status;
    }

    private async Task<AppUpdateStatus> CheckPortableAsync(bool manual, CancellationToken cancellationToken)
    {
        var state = LoadPortableState();
        if (!manual && state.LastCheckedUtc is { } checkedAt &&
            DateTimeOffset.UtcNow - checkedAt < PortableCheckCooldown)
        {
            return Status = Status with { State = AppUpdateCheckState.Idle, Message = "The next automatic update check is not due yet." };
        }

        using var response = await _http.GetAsync(LatestReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var tag = body.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
        var version = tag.TrimStart('v', 'V');
        var releaseUrl = body.RootElement.GetProperty("html_url").GetString();
        SavePortableState(state with { LastCheckedUtc = DateTimeOffset.UtcNow });

        if (!IsNewer(version, _currentVersion))
        {
            return Status = new(Distribution, AppUpdateCheckState.UpToDate, null, "SyncSentinel is up to date.", null);
        }

        Status = new(Distribution, AppUpdateCheckState.UpdateAvailable, version,
            $"SyncSentinel {version} is available. Portable copies are updated manually.", releaseUrl);

        if (manual || !string.Equals(state.SkippedVersion, version, StringComparison.OrdinalIgnoreCase))
        {
            ShowPortablePrompt(version, releaseUrl, state);
        }
        return Status;
    }

    private void ShowPortablePrompt(string version, string? releaseUrl, PortableUpdateState state)
    {
        _form!.BeginInvoke(() =>
        {
            var choice = MessageBox.Show(
                $"SyncSentinel {version} is available. This is a portable copy, so it will never overwrite itself.\n\n"
                + "Yes — view the release\nNo — skip this version\nCancel — remind me later",
                "SyncSentinel update available",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Information);

            if (choice == DialogResult.Yes && releaseUrl is not null)
            {
                Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
            }
            else if (choice == DialogResult.No)
            {
                SavePortableState(state with { SkippedVersion = version, LastCheckedUtc = DateTimeOffset.UtcNow });
            }
        });
    }

    private PortableUpdateState LoadPortableState()
    {
        try
        {
            return File.Exists(_portableStatePath)
                ? JsonSerializer.Deserialize<PortableUpdateState>(File.ReadAllText(_portableStatePath)) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }

    private void SavePortableState(PortableUpdateState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_portableStatePath)!);
        File.WriteAllText(_portableStatePath, JsonSerializer.Serialize(state));
    }

    private static bool IsNewer(string candidate, string current) =>
        Version.TryParse(candidate.Split('-', '+')[0], out var candidateVersion) &&
        Version.TryParse(current.Split('-', '+')[0], out var currentVersion) &&
        candidateVersion > currentVersion;

    private void ShowMessage(string message, string caption, MessageBoxIcon icon) =>
        _form?.BeginInvoke(() => MessageBox.Show(message, caption, MessageBoxButtons.OK, icon));

    public void Dispose()
    {
        _sparkle?.Dispose();
        _retryTimer?.Dispose();
        _http.Dispose();
        _checkGate.Dispose();
    }

    private sealed record PortableUpdateState
    {
        public DateTimeOffset? LastCheckedUtc { get; init; }
        public string? SkippedVersion { get; init; }
    }
}
