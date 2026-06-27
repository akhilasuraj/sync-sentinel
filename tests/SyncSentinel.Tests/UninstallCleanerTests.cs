using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class UninstallCleanerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ss-uninst-" + Guid.NewGuid().ToString("N"));
    private readonly FakeAutostart _autostart = new();

    private UninstallCleaner New() => new(new StoragePaths(_root), _autostart);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Clean_always_disables_autostart()
    {
        New().Clean(purgeData: false);

        Assert.Equal(false, _autostart.LastApplied);
    }

    [Fact]
    public void Clean_with_purgeData_deletes_the_data_root()
    {
        Directory.CreateDirectory(Path.Combine(_root, "logs"));
        File.WriteAllText(Path.Combine(_root, "config.json"), "{}");

        New().Clean(purgeData: true);

        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void Clean_without_purgeData_keeps_the_data_root()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "config.json"), "{}");

        New().Clean(purgeData: false);

        Assert.True(Directory.Exists(_root));
        Assert.True(File.Exists(Path.Combine(_root, "config.json")));
    }

    [Fact]
    public void Clean_with_purgeData_when_root_absent_does_not_throw()
    {
        // No data root created (fresh install never run, or already purged).
        var ex = Record.Exception(() => New().Clean(purgeData: true));

        Assert.Null(ex);
    }

    private sealed class FakeAutostart : IAutostart
    {
        public bool? LastApplied;
        public void Apply(bool enabled) => LastApplied = enabled;
    }
}
