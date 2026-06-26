using SyncSentinel.Core;

namespace SyncSentinel;

/// <summary>
/// Phase-1 placeholder job source: a self-contained demo under %TEMP% so
/// "Run now" exercises the real robocopy pipeline without touching real data.
/// Each call rebuilds a fresh source tree and clears the destination, so every
/// run performs a full, visible mirror (the point is to watch the live log +
/// status). Phase 2 replaces this with the persisted job store (config.json)
/// pointing at real folders.
/// </summary>
internal sealed class DemoJobSource : IBackupJobSource
{
    public BackupJob GetCurrent()
    {
        var root = Path.Combine(Path.GetTempPath(), "SyncSentinelDemo");
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "backup");

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
        SeedDemoTree(source);

        return new BackupJob
        {
            Name = "Demo (%TEMP%\\SyncSentinelDemo)",
            Source = source,
            Destination = destination,
            ExcludeFolders = ["bin"],
            // Show per-file lines for the demo (drop /NFL) so the live log
            // visibly streams as files copy. Real jobs use the global default.
            Flags = "/MIR /XJ /R:1 /W:1 /FFT /NP",
        };
    }

    private static void SeedDemoTree(string source)
    {
        File.WriteAllText(Path.Combine(EnsureDir(source), "readme.txt"), "SyncSentinel demo");

        // ~180 small files across a few folders so the log clearly scrolls.
        foreach (var folder in new[] { "docs", "data", "assets" })
        {
            var dir = EnsureDir(Path.Combine(source, folder));
            for (var i = 1; i <= 60; i++)
            {
                File.WriteAllText(
                    Path.Combine(dir, $"{folder}-{i:000}.txt"),
                    $"demo file {folder}/{i}\n" + new string('x', 3072));
            }
        }

        // A few larger files so a fresh mirror takes a visible beat.
        var media = EnsureDir(Path.Combine(source, "media"));
        var oneMb = new byte[1024 * 1024];
        for (var i = 1; i <= 12; i++)
        {
            File.WriteAllBytes(Path.Combine(media, $"clip-{i:00}.bin"), oneMb);
        }

        // An excluded build folder, to show /XD in action.
        File.WriteAllText(
            Path.Combine(EnsureDir(Path.Combine(source, "bin")), "ignored.dll"), "binary");
    }

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
