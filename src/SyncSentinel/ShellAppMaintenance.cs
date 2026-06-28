using System.Diagnostics;
using System.Threading.Tasks;
using SyncSentinel.Core;

namespace SyncSentinel;

/// <summary>
/// Shell implementation of <see cref="IAppMaintenance"/>: the portable "remove all
/// data &amp; quit" action. Spawns a detached helper running the same proven
/// <c>--uninstall --purge-data</c> path the installer uses (clears the data root +
/// the autostart entry), then quits this instance. The helper retries the delete
/// briefly, so it succeeds the moment this process exits and releases history.db.
/// The running executable can't delete itself — the UI tells the user to remove it.
/// </summary>
internal sealed class ShellAppMaintenance : IAppMaintenance
{
    private readonly string _exePath;
    private MainForm? _form;

    public ShellAppMaintenance(string exePath) => _exePath = exePath;

    /// <summary>Wire the window once it exists (the DI container is built first).</summary>
    public void SetForm(MainForm form) => _form = form;

    public void WipeDataAndQuit()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_exePath, "--uninstall --purge-data")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch
        {
            // Best-effort: even if the helper fails to launch, still quit so the
            // user isn't stuck; the data simply remains.
        }

        // Quit on the UI thread; this releases the host + history.db so the helper's
        // retry can complete the deletion.
        var quitting = false;
        try
        {
            if (_form is { IsHandleCreated: true })
            {
                _form.BeginInvoke(() => _form!.ExitApplication());
                quitting = true;
            }
        }
        catch
        {
            // Fall through to the force-exit backstop below.
        }

        // Backstop: if we couldn't quit gracefully (no window / BeginInvoke failed),
        // force-exit shortly after — by then the 202 response has flushed — so the
        // helper's retry finds history.db unlocked. The exit also frees the lock
        // even if graceful shutdown stalls.
        if (!quitting)
        {
            Task.Run(async () =>
            {
                await Task.Delay(800);
                Environment.Exit(0);
            });
        }
    }
}
