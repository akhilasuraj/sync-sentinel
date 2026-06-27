using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class RunPreconditionsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ss-pre-" + Guid.NewGuid().ToString("N"));

    public RunPreconditionsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // A drive root that does not exist on this machine (scans Z: down to D:).
    private static string FirstMissingDriveRoot()
    {
        for (var c = 'Z'; c >= 'D'; c--)
        {
            var root = $"{c}:\\";
            if (!Directory.Exists(root))
            {
                return root;
            }
        }
        throw new InvalidOperationException("no free drive letter to test with");
    }

    [Fact]
    public void Reports_a_missing_source()
    {
        var reason = RunPreconditions.Check(Path.Combine(_dir, "nope"), Path.Combine(_dir, "dst"));

        Assert.NotNull(reason);
        Assert.Contains("Source", reason);
    }

    [Fact]
    public void Reports_a_missing_destination_drive()
    {
        var src = Path.Combine(_dir, "src");
        Directory.CreateDirectory(src);

        var reason = RunPreconditions.Check(src, FirstMissingDriveRoot() + "Backup");

        Assert.NotNull(reason);
        Assert.Contains("drive", reason);
    }

    [Fact]
    public void Allows_a_run_when_source_exists_and_only_the_dest_folder_is_missing()
    {
        var src = Path.Combine(_dir, "src");
        Directory.CreateDirectory(src);

        // The destination folder doesn't exist yet, but its drive (the scratch
        // drive) does — robocopy creates the folder, so this is allowed.
        Assert.Null(RunPreconditions.Check(src, Path.Combine(_dir, "dst")));
    }
}
