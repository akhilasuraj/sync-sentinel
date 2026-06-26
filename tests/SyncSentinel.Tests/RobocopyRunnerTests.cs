using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class RobocopyRunnerTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-test-" + Guid.NewGuid().ToString("N"));

    public RobocopyRunnerTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public async Task RunAsync_mirrors_included_files_excludes_folders_and_streams_output()
    {
        var src = Path.Combine(_scratch, "src");
        var dst = Path.Combine(_scratch, "dst");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "hello");
        Directory.CreateDirectory(Path.Combine(src, "bin"));
        File.WriteAllText(Path.Combine(src, "bin", "skip.dll"), "x");

        var job = new BackupJob { Source = src, Destination = dst, ExcludeFolders = ["bin"] };

        var lines = new List<string>();
        var runner = new RobocopyRunner();
        var result = await runner.RunAsync(job, line => { lock (lines) { lines.Add(line); } });

        Assert.True(File.Exists(Path.Combine(dst, "a.txt")), "a.txt should be mirrored");
        Assert.False(Directory.Exists(Path.Combine(dst, "bin")), "bin/ should be excluded");
        Assert.Equal(RobocopyStatus.Success, result.Status);
        Assert.NotEmpty(lines);
    }
}
