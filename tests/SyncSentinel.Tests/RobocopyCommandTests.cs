using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public class RobocopyCommandTests
{
    [Fact]
    public void Build_starts_with_source_then_destination()
    {
        var job = new BackupJob { Source = @"C:\src", Destination = @"D:\dst" };

        var args = RobocopyCommand.Build(job);

        Assert.Equal(@"C:\src", args[0]);
        Assert.Equal(@"D:\dst", args[1]);
    }

    [Fact]
    public void Build_appends_each_behavior_flag_as_its_own_token()
    {
        var job = new BackupJob { Source = "s", Destination = "d", Flags = "/MIR /XJ /R:3" };

        var args = RobocopyCommand.Build(job);

        Assert.Contains("/MIR", args);
        Assert.Contains("/XJ", args);
        Assert.Contains("/R:3", args);
    }

    [Fact]
    public void Build_adds_XD_followed_by_each_excluded_folder()
    {
        var job = new BackupJob
        {
            Source = "s", Destination = "d", Flags = "",
            ExcludeFolders = ["bin", "obj"],
        };

        var args = RobocopyCommand.Build(job).ToList();

        var xd = args.IndexOf("/XD");
        Assert.True(xd >= 0, "expected a /XD token");
        Assert.Equal("bin", args[xd + 1]);
        Assert.Equal("obj", args[xd + 2]);
    }

    [Fact]
    public void Build_adds_XF_followed_by_each_excluded_file_pattern()
    {
        var job = new BackupJob
        {
            Source = "s", Destination = "d", Flags = "",
            ExcludeFiles = ["*.dll", "*.pdb"],
        };

        var args = RobocopyCommand.Build(job).ToList();

        var xf = args.IndexOf("/XF");
        Assert.True(xf >= 0, "expected a /XF token");
        Assert.Equal("*.dll", args[xf + 1]);
        Assert.Equal("*.pdb", args[xf + 2]);
    }

    [Fact]
    public void Build_omits_XD_and_XF_when_there_are_no_excludes()
    {
        var job = new BackupJob { Source = "s", Destination = "d" };

        var args = RobocopyCommand.Build(job);

        Assert.DoesNotContain("/XD", args);
        Assert.DoesNotContain("/XF", args);
    }
}
