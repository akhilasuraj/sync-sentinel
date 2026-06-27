using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ss-cfg-" + Guid.NewGuid().ToString("N"));

    public ConfigStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Load_on_empty_dir_seeds_defaults_and_writes_them_to_disk()
    {
        var config = new ConfigStore(_dir).Load();

        Assert.Contains(config.FolderSets, s => s.Name == "Developer Defaults");
        Assert.True(File.Exists(Path.Combine(_dir, "config.json")), "the seed should be persisted");
    }

    [Fact]
    public void Save_then_Load_round_trips_the_config()
    {
        var config = new SyncSentinelConfig
        {
            Jobs =
            [
                new Job
                {
                    Id = "j", Name = "J", Source = "s", Destination = "d",
                    FolderSetIds = ["x"], FlagsOverride = "/MIR", IntervalMinutes = 30, Enabled = true,
                },
            ],
            FolderSets = [new FolderExclusionSet { Id = "x", Name = "X", Folders = ["bin", "obj"] }],
        };

        new ConfigStore(_dir).Save(config);
        var loaded = new ConfigStore(_dir).Load();

        Assert.Single(loaded.Jobs);
        Assert.Equal("J", loaded.Jobs[0].Name);
        Assert.Equal(30, loaded.Jobs[0].IntervalMinutes);
        Assert.Equal(["bin", "obj"], loaded.FolderSets[0].Folders);
    }
}
