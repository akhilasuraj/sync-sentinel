using NetSparkleUpdater.AppCastHandlers;
using NetSparkleUpdater.Configurations;
using NetSparkleUpdater.Interfaces;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class InstalledUpdaterBoundaryTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-installed-updater-" + Guid.NewGuid().ToString("N"));

    public InstalledUpdaterBoundaryTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public void NetSparkle_treats_the_normalized_installed_version_as_the_current_feed_version()
    {
        var profile = InstalledUpdateProfile.Create(
            new StoragePaths(_scratch),
            "0.6.0+b97340159939");

        var installed = SemVerLike.Parse(profile.CurrentVersion);

        Assert.Equal(0, SemVerLike.Parse("0.6.0").CompareTo(installed));
        Assert.True(SemVerLike.Parse("0.6.1").CompareTo(installed) > 0);
    }

    [Fact]
    public void NetSparkle_skip_and_check_state_round_trips_under_the_application_data_root()
    {
        var profile = InstalledUpdateProfile.Create(new StoragePaths(_scratch), "1.0.0");
        var accessor = new TestAssemblyAccessor(profile.CurrentVersion);
        var configuration = new JSONConfiguration(accessor, profile.StatePath);

        configuration.SetVersionToSkip("1.1.0");
        configuration.TouchCheckTime();

        var reloaded = new JSONConfiguration(accessor, profile.StatePath);
        Assert.Equal("1.1.0", reloaded.LastVersionSkipped);
        Assert.NotEqual(default, reloaded.LastCheckTime);
        Assert.StartsWith(Path.GetFullPath(_scratch), Path.GetFullPath(profile.StatePath));
    }

    private sealed class TestAssemblyAccessor(string version) : IAssemblyAccessor
    {
        public string AssemblyVersion => version;
        public string AssemblyTitle => "SyncSentinel";
        public string AssemblyDescription => "SyncSentinel updater test";
        public string AssemblyProduct => "SyncSentinel";
        public string AssemblyCompany => "SyncSentinel";
        public string AssemblyCopyright => string.Empty;
    }
}
