using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ss-svc-" + Guid.NewGuid().ToString("N"));

    public ConfigServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ConfigService NewService() => new(new ConfigStore(_dir));

    [Fact]
    public void Loads_the_seeded_config_on_construction()
    {
        var service = NewService();

        Assert.Contains(service.Current.FolderSets, s => s.Name == "Developer Defaults");
    }

    private static Job NewJob(string id = "") => new()
    {
        Id = id, Name = "PEMS", Source = @"C:\dev\PEMS", Destination = @"D:\bak\PEMS",
    };

    [Fact]
    public void AddJob_assigns_an_id_and_persists_so_a_reload_sees_it()
    {
        var added = NewService().AddJob(NewJob());

        Assert.False(string.IsNullOrEmpty(added.Id));
        // A fresh service over the same dir must see the persisted job.
        Assert.Contains(NewService().Current.Jobs, j => j.Id == added.Id && j.Name == "PEMS");
    }

    [Fact]
    public void ResolveJob_returns_the_effective_backup_job()
    {
        var service = NewService();
        var added = service.AddJob(NewJob() with { FolderSetIds = [DefaultConfig.DeveloperDefaultsId] });

        var resolved = service.ResolveJob(added.Id);

        Assert.NotNull(resolved);
        Assert.Equal(@"C:\dev\PEMS", resolved!.Source);
        Assert.Contains("node_modules", resolved.ExcludeFolders); // from DeveloperDefaults
    }

    [Fact]
    public void DeleteJob_removes_it_and_persists()
    {
        var service = NewService();
        var added = service.AddJob(NewJob());

        var deleted = service.DeleteJob(added.Id);

        Assert.True(deleted);
        Assert.DoesNotContain(NewService().Current.Jobs, j => j.Id == added.Id);
    }

    [Fact]
    public void UpdateJob_replaces_by_id_and_persists()
    {
        var service = NewService();
        var added = service.AddJob(NewJob());

        var ok = service.UpdateJob(added with { Name = "Renamed" });

        Assert.True(ok);
        Assert.Contains(NewService().Current.Jobs, j => j.Id == added.Id && j.Name == "Renamed");
    }
}
