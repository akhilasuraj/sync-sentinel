using System.Diagnostics;
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
        _form?.BeginInvoke(() => _form!.ExitApplication());
    }
}
