namespace SyncSentinel.Core;

/// <summary>
/// The first-run seed. Generic and free of personal data (this is a public
/// repo): just the universal <c>Developer Defaults</c> folder-set and default
/// settings — no jobs. Real jobs are created through the UI (a friendly empty
/// state guides the first one) and live only in the user's %APPDATA% config.
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
                Name = "Developer Defaults",
                Folders =
                [
                    "bin", "obj", "dist", "build", "out", "node_modules", "packages",
                    ".next", "target", "vendor", "__pycache__", ".venv", "venv", ".vs",
                ],
            },
        ],
        Jobs = [],
        Settings = new GlobalSettings(),
    };
}
