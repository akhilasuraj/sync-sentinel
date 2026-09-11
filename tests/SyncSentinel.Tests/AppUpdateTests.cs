using SyncSentinel.Core;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SyncSentinel.Tests;

public sealed class AppUpdateTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-update-" + Guid.NewGuid().ToString("N"));

    public AppUpdateTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public void Distribution_is_installed_only_when_the_Inno_uninstaller_is_adjacent()
    {
        var exe = Path.Combine(_scratch, "SyncSentinel.exe");

        Assert.Equal(AppDistribution.Portable, AppInstallation.Detect(exe));

        File.WriteAllText(Path.Combine(_scratch, "unins000.exe"), "fixture");

        Assert.Equal(AppDistribution.Installed, AppInstallation.Detect(exe));
    }

    [Fact]
    public void Update_install_is_blocked_while_a_run_is_queued_or_running()
    {
        var queue = new RunQueue();
        var guard = new UpdateInstallGuard(queue);

        Assert.True(guard.CanInstall(out _));
        queue.Enqueue("job-1");
        Assert.False(guard.CanInstall(out var queuedReason));
        Assert.Contains("backup", queuedReason, StringComparison.OrdinalIgnoreCase);
        queue.Dequeue();
        Assert.False(guard.CanInstall(out _));
        queue.Complete("job-1");
        Assert.True(guard.CanInstall(out _));
    }

    [Fact]
    public async Task Update_endpoints_expose_capability_status_and_manual_checks()
    {
        var updater = new FakeUpdater();
        await using var app = await TestApp.StartAsync(
            Path.Combine(_scratch, "config"),
            services => services.AddSingleton<IAppUpdateService>(updater));
        var client = app.GetTestClient();

        var caps = await client.GetFromJsonAsync<Capabilities>("/api/capabilities");
        Assert.Equal("portable", caps!.Updates);

        var initial = await client.GetFromJsonAsync<AppUpdateStatus>("/api/updates/status");
        Assert.Equal(AppUpdateCheckState.Idle, initial!.State);

        var response = await client.PostAsync("/api/updates/check", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(updater.LastCheckWasManual);
        var checkedStatus = await response.Content.ReadFromJsonAsync<AppUpdateStatus>();
        Assert.Equal(AppUpdateCheckState.UpToDate, checkedStatus!.State);
    }

    [Fact]
    public void Missing_update_setting_deserializes_as_enabled_for_existing_users()
    {
        File.WriteAllText(Path.Combine(_scratch, "config.json"), """
        { "jobs": [], "folderSets": [], "fileSets": [], "settings": { "autostart": false } }
        """);

        var config = new ConfigStore(_scratch).Load();

        Assert.True(config.Settings.AutomaticUpdateChecks);
    }

    [Fact]
    public void Release_pipeline_publishes_a_signed_appcast_for_the_silent_installer()
    {
        var root = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        var installer = File.ReadAllText(Path.Combine(root, "installer", "SyncSentinel.iss"));

        Assert.Contains("SPARKLE_PRIVATE_KEY", workflow);
        Assert.Contains("netsparkle-generate-appcast", workflow);
        Assert.Contains("appcast.xml.signature", workflow);
        Assert.Contains("SyncSentinel-Setup.exe", workflow);
        Assert.Contains("skipifsilent", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--quit", installer);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SyncSentinel.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    private sealed record Capabilities(string Updates);

    private sealed class FakeUpdater : IAppUpdateService
    {
        public AppDistribution Distribution => AppDistribution.Portable;
        public bool Available => true;
        public bool LastCheckWasManual { get; private set; }
        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.Idle(AppDistribution.Portable);

        public Task<AppUpdateStatus> CheckAsync(bool manual, CancellationToken cancellationToken = default)
        {
            LastCheckWasManual = manual;
            Status = new(AppDistribution.Portable, AppUpdateCheckState.UpToDate, null, "SyncSentinel is up to date.", null);
            return Task.FromResult(Status);
        }
    }
}
