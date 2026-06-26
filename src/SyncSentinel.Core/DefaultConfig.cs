namespace SyncSentinel.Core;

/// <summary>
/// The first-run seed. Generic and free of personal data (this is a public
/// repo): the universal <c>DeveloperDefaults</c> folder-set, default settings,
/// and one disabled example job with placeholder paths. Real jobs are added
/// through the UI and live only in the user's %APPDATA% config.
/// </summary>
public static class DefaultConfig
{
    public const string DeveloperDefaultsId = "developer-defaults";

    public static SyncSentinelConfig Seed() => new()
    {
        FolderSets =
        [
            new FolderExclusionSet
            {
                Id = DeveloperDefaultsId,
                Name = "DeveloperDefaults",
                Folders =
                [
                    "bin", "obj", "dist", "build", "out", "node_modules", "packages",
                    ".next", "target", "vendor", "__pycache__", ".venv", "venv", ".vs",
                ],
            },
        ],
        Jobs =
        [
            new Job
            {
                Id = "example",
                Name = "Example",
                Source = @"C:\dev\Example",
                Destination = @"D:\Backup\Example",
                FolderSetIds = [DeveloperDefaultsId],
                Enabled = false,
            },
        ],
        Settings = new GlobalSettings(),
    };
}
