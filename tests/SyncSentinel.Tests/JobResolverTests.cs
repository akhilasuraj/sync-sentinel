using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public class JobResolverTests
{
    private static Job SampleJob() => new()
    {
        Id = "j1",
        Name = "PEMS",
        Source = @"C:\dev\PEMS",
        Destination = @"D:\bak\PEMS",
    };

    [Fact]
    public void Resolve_carries_over_name_source_and_destination()
    {
        var resolved = JobResolver.Resolve(SampleJob(), new SyncSentinelConfig());

        Assert.Equal("PEMS", resolved.Name);
        Assert.Equal(@"C:\dev\PEMS", resolved.Source);
        Assert.Equal(@"D:\bak\PEMS", resolved.Destination);
    }

    [Fact]
    public void Resolve_uses_global_default_flags_when_job_has_no_override()
    {
        var config = new SyncSentinelConfig { Settings = new GlobalSettings { DefaultFlags = "/MIR /Z" } };

        var resolved = JobResolver.Resolve(SampleJob() with { FlagsOverride = null }, config);

        Assert.Equal("/MIR /Z", resolved.Flags);
    }

    [Fact]
    public void Resolve_uses_the_jobs_flags_override_when_set()
    {
        var config = new SyncSentinelConfig { Settings = new GlobalSettings { DefaultFlags = "/MIR /Z" } };

        var resolved = JobResolver.Resolve(SampleJob() with { FlagsOverride = "/MIR /R:1" }, config);

        Assert.Equal("/MIR /R:1", resolved.Flags);
    }

    [Fact]
    public void Resolve_unions_attached_folder_sets_into_exclude_folders()
    {
        var config = new SyncSentinelConfig
        {
            FolderSets =
            [
                new FolderExclusionSet { Id = "dotnet", Name = "DotNet", Folders = ["bin", "obj"] },
                new FolderExclusionSet { Id = "node", Name = "Node", Folders = ["node_modules"] },
            ],
        };

        var resolved = JobResolver.Resolve(SampleJob() with { FolderSetIds = ["dotnet", "node"] }, config);

        Assert.Equal(["bin", "obj", "node_modules"], resolved.ExcludeFolders);
    }

    [Fact]
    public void Resolve_unions_attached_file_sets_into_exclude_files()
    {
        var config = new SyncSentinelConfig
        {
            FileSets =
            [
                new FileExclusionSet { Id = "bin", Name = "Binaries", Patterns = ["*.dll", "*.exe"] },
            ],
        };

        var resolved = JobResolver.Resolve(SampleJob() with { FileSetIds = ["bin"] }, config);

        Assert.Equal(["*.dll", "*.exe"], resolved.ExcludeFiles);
    }

    [Fact]
    public void Resolve_deduplicates_overlapping_sets_and_ignores_unknown_ids()
    {
        var config = new SyncSentinelConfig
        {
            FolderSets =
            [
                new FolderExclusionSet { Id = "a", Name = "A", Folders = ["bin", "obj"] },
                new FolderExclusionSet { Id = "b", Name = "B", Folders = ["obj", "out"] },
            ],
        };

        // 'ghost' was deleted but the job still references it — must not throw.
        var resolved = JobResolver.Resolve(SampleJob() with { FolderSetIds = ["a", "b", "ghost"] }, config);

        Assert.Equal(["bin", "obj", "out"], resolved.ExcludeFolders);
    }
}
