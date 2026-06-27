namespace SyncSentinel.Core;

/// <summary>
/// Removes SyncSentinel's footprint on uninstall: always clears login autostart
/// (the HKCU Run entry) via <see cref="IAutostart"/>. The single source of truth for
/// what an uninstall removes — the installer just invokes the <c>--uninstall</c> CLI,
/// which calls this. Composes the same <see cref="IAutostart"/> and
/// <see cref="StoragePaths"/> the running app uses, so it can never drift from where
/// autostart and data actually live.
/// </summary>
public sealed class UninstallCleaner
{
    private readonly StoragePaths _paths;
    private readonly IAutostart _autostart;

    public UninstallCleaner(StoragePaths paths, IAutostart autostart)
    {
        _paths = paths;
        _autostart = autostart;
    }

    /// <summary>
    /// Clear login autostart, and when <paramref name="purgeData"/> is set also delete
    /// the data root (config, history, logs).
    /// </summary>
    public void Clean(bool purgeData)
    {
        _autostart.Apply(false);

        if (purgeData && Directory.Exists(_paths.Root))
        {
            Directory.Delete(_paths.Root, recursive: true);
        }
    }
}
