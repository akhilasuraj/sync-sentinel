using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;
using SyncSentinel.Core;

namespace SyncSentinel;

/// <summary>Native NetSparkle adapter for cryptographically signed installed updates.</summary>
internal sealed class InstalledUpdateService : IAppUpdateService, IDisposable
{
    private const string AppcastUrl =
        "https://github.com/akhilasuraj/sync-sentinel/releases/latest/download/appcast.xml";
    private const string PublicKey = "nEDaY7ezIENToBpyirIB7a/EK+LTwW8qBUvF7WOjxzM=";

    private readonly UpdateInstallGuard _installGuard;
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private MainForm? _form;
    private SparkleUpdater? _sparkle;
    private System.Windows.Forms.Timer? _retryTimer;
    private AppCastItem? _deferredUpdate;

    public InstalledUpdateService(RunQueue queue)
    {
        _installGuard = new UpdateInstallGuard(queue);
        Status = AppUpdateStatus.Idle(Distribution);
    }

    public AppDistribution Distribution => AppDistribution.Installed;
    public bool Available => _form is not null;
    public AppUpdateStatus Status { get; private set; }

    public void SetForm(MainForm form)
    {
        _form = form;
        _sparkle = new SparkleUpdater(AppcastUrl, new Ed25519Checker(SecurityMode.Strict, PublicKey))
        {
            UIFactory = new NetSparkleUpdater.UI.WinForms.UIFactory(form.Icon),
            RelaunchAfterUpdate = true,
            CustomInstallerArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
        };
        _retryTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _retryTimer.Tick += async (_, _) =>
        {
            if (!_installGuard.TryReserve(out _))
            {
                return;
            }
            _installGuard.Release();
            _retryTimer.Stop();
            var deferred = _deferredUpdate;
            if (deferred is not null)
            {
                await _sparkle!.InstallUpdate(deferred);
            }
        };
        _sparkle.UserRespondedToUpdate += (_, eventArgs) =>
        {
            if (eventArgs.Result == UpdateAvailableResult.InstallUpdate)
            {
                _deferredUpdate = eventArgs.UpdateItem;
            }
        };
        _sparkle.PreparingToExit += (_, eventArgs) =>
        {
            if (_installGuard.TryReserve(out var reason))
            {
                return;
            }

            eventArgs.Cancel = true;
            _retryTimer.Start();
            form.BeginInvoke(() => MessageBox.Show(
                reason, "Update waiting", MessageBoxButtons.OK, MessageBoxIcon.Information));
            Status = Status with { Message = reason };
        };
        _sparkle.InstallUpdateFailed += (_, _) =>
        {
            _installGuard.Release();
            return true;
        };
        _sparkle.CloseApplication += () =>
        {
            _deferredUpdate = null;
            form.BeginInvoke(form.ExitApplication);
        };
    }

    public async Task<AppUpdateStatus> CheckAsync(
        UpdateCheckMode mode,
        CancellationToken cancellationToken = default)
    {
        if (_sparkle is null)
        {
            return Status = Status with
            {
                State = AppUpdateCheckState.Error,
                Message = "The desktop window is not ready yet.",
            };
        }

        await _checkGate.WaitAsync(cancellationToken);
        try
        {
            Status = Status with { State = AppUpdateCheckState.Checking, Message = "Checking for updates…" };
            var manual = mode == UpdateCheckMode.UserRequested;
            var info = manual
                ? await _sparkle.CheckForUpdatesAtUserRequest(ignoreSkippedVersions: true)
                : await _sparkle.CheckForUpdatesQuietly();
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

    public void Dispose()
    {
        _sparkle?.Dispose();
        _retryTimer?.Dispose();
        _checkGate.Dispose();
    }
}
