namespace SyncSentinel.Core;

/// <summary>
/// Neutral seam for app-lifecycle maintenance the API can request but only the
/// WinForms shell can carry out. Today: wiping the app's footprint (data root +
/// autostart entry) and quitting — the portable-build equivalent of the
/// installer's uninstall+purge. Core stays free of process/registry concerns;
/// tests inject a fake, and the shell provides the real implementation.
/// </summary>
public interface IAppMaintenance
{
    /// <summary>
    /// Remove all SyncSentinel data and the login-autostart entry, then quit.
    /// Cannot delete the running executable itself.
    /// </summary>
    void WipeDataAndQuit();
}

/// <summary>
/// Default <see cref="IAppMaintenance"/> that does nothing — registered in the
/// shared wiring so the endpoint resolves under the in-memory TestServer and on
/// non-shell hosts. The shell overrides it with a real implementation.
/// </summary>
public sealed class NoOpAppMaintenance : IAppMaintenance
{
    public void WipeDataAndQuit() { }
}
