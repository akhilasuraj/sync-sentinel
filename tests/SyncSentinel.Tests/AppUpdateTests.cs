using SyncSentinel.Core;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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

        Assert.True(guard.TryReserve(out _));
        Assert.False(queue.Enqueue("blocked-by-update"));
        guard.Release();
        queue.Enqueue("job-1");
        Assert.False(guard.TryReserve(out var queuedReason));
        Assert.Contains("backup", queuedReason, StringComparison.OrdinalIgnoreCase);
        queue.Dequeue();
        Assert.False(guard.TryReserve(out _));
        queue.Complete("job-1");
        Assert.True(guard.TryReserve(out _));
        guard.Release();
    }

    [Theory]
    [InlineData("1.2.4", "1.2.3", true)]
    [InlineData("1.2.3", "1.2.3", false)]
    [InlineData("1.2.2", "1.2.3", false)]
    [InlineData("v2.0.0", "1.9.9", true)]
    public void Portable_version_comparison_only_accepts_newer_semantic_versions(
        string candidate, string current, bool expected) =>
        Assert.Equal(expected, PortableUpdatePolicy.IsNewer(candidate, current));

    [Fact]
    public void Manual_portable_checks_bypass_cooldown_and_skipped_version()
    {
        var now = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

        Assert.False(PortableUpdatePolicy.ShouldCheck(now.AddMinutes(-5), now, manual: false));
        Assert.True(PortableUpdatePolicy.ShouldCheck(now.AddMinutes(-5), now, manual: true));
        Assert.False(PortableUpdatePolicy.ShouldPrompt("2.0.0", "2.0.0", manual: false));
        Assert.True(PortableUpdatePolicy.ShouldPrompt("2.0.0", "2.0.0", manual: true));
    }

    [Fact]
    public async Task Portable_service_persists_skip_and_manual_check_reveals_it_again()
    {
        var source = new FakeReleaseSource(new UpdateRelease("2.0.0", "https://example.test/v2"));
        var store = new MemoryUpdateStateStore();
        var interaction = new FakePortableInteraction(PortableUpdateChoice.SkipVersion);
        var service = new PortableUpdateService("1.0.0", source, store, interaction);

        var automatic = await service.CheckAsync(UpdateCheckMode.Automatic);
        Assert.Equal(AppUpdateCheckState.UpdateAvailable, automatic.State);
        Assert.Equal("2.0.0", store.State.SkippedVersion);

        interaction.Choice = PortableUpdateChoice.RemindLater;
        var manual = await service.CheckAsync(UpdateCheckMode.UserRequested);
        Assert.Equal(AppUpdateCheckState.UpdateAvailable, manual.State);
        Assert.Equal(2, interaction.PromptCount);
    }

    [Fact]
    public async Task Portable_service_reports_feed_errors_without_prompting()
    {
        var interaction = new FakePortableInteraction(PortableUpdateChoice.RemindLater);
        var service = new PortableUpdateService(
            "1.0.0", new FakeReleaseSource(new HttpRequestException("offline")),
            new MemoryUpdateStateStore(), interaction);

        var result = await service.CheckAsync(UpdateCheckMode.Automatic);

        Assert.Equal(AppUpdateCheckState.Error, result.State);
        Assert.Equal(0, interaction.PromptCount);
    }

    [Fact]
    public async Task GitHub_release_source_reads_the_latest_tag_and_release_url()
    {
        var handler = new StubHttpHandler("""
            { "tag_name": "v2.3.4", "html_url": "https://github.com/example/releases/tag/v2.3.4" }
            """);
        var source = new GitHubUpdateReleaseSource(new HttpClient(handler));

        var release = await source.GetLatestAsync();

        Assert.Equal("2.3.4", release.Version);
        Assert.Equal("https://github.com/example/releases/tag/v2.3.4", release.ReleaseUrl);
        Assert.Equal("api.github.com", handler.RequestUri!.Host);
        Assert.Contains("SyncSentinel", handler.UserAgent);
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
    public async Task Startup_and_live_setting_respect_the_automatic_check_preference()
    {
        var updater = new FakeUpdater();
        await UpdateStartup.CheckAsync(new GlobalSettings { AutomaticUpdateChecks = false }, updater);
        Assert.Equal(0, updater.CheckCount);
        await UpdateStartup.CheckAsync(new GlobalSettings { AutomaticUpdateChecks = true }, updater);
        Assert.Equal(UpdateCheckMode.Automatic, updater.LastMode);

        await using var app = await TestApp.StartAsync(
            Path.Combine(_scratch, "live-setting"),
            services => services.AddSingleton<IAppUpdateService>(updater));
        var client = app.GetTestClient();
        var config = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");
        await client.PutAsJsonAsync("/api/settings", config!.Settings with { AutomaticUpdateChecks = false });
        var beforeEnable = updater.CheckCount;

        await client.PutAsJsonAsync("/api/settings", config.Settings with { AutomaticUpdateChecks = true });

        Assert.Equal(beforeEnable + 1, updater.CheckCount);
        Assert.Equal(UpdateCheckMode.Automatic, updater.LastMode);
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
        public int CheckCount { get; private set; }
        public UpdateCheckMode? LastMode { get; private set; }
        public AppUpdateStatus Status { get; private set; } = AppUpdateStatus.Idle(AppDistribution.Portable);

        public Task<AppUpdateStatus> CheckAsync(UpdateCheckMode mode, CancellationToken cancellationToken = default)
        {
            CheckCount++;
            LastMode = mode;
            LastCheckWasManual = mode == UpdateCheckMode.UserRequested;
            Status = new(AppDistribution.Portable, AppUpdateCheckState.UpToDate, null, "SyncSentinel is up to date.", null);
            return Task.FromResult(Status);
        }
    }

    private sealed class FakeReleaseSource : IUpdateReleaseSource
    {
        private readonly UpdateRelease? _release;
        private readonly Exception? _error;
        public FakeReleaseSource(UpdateRelease release) => _release = release;
        public FakeReleaseSource(Exception error) => _error = error;
        public Task<UpdateRelease> GetLatestAsync(CancellationToken cancellationToken = default) =>
            _error is null ? Task.FromResult(_release!) : Task.FromException<UpdateRelease>(_error);
    }

    private sealed class MemoryUpdateStateStore : IPortableUpdateStateStore
    {
        public PortableUpdateState State { get; private set; } = new();
        public PortableUpdateState Load() => State;
        public void Save(PortableUpdateState state) => State = state;
    }

    private sealed class FakePortableInteraction(PortableUpdateChoice choice) : IPortableUpdateInteraction
    {
        public PortableUpdateChoice Choice { get; set; } = choice;
        public int PromptCount { get; private set; }
        public Task<PortableUpdateChoice> PromptAsync(UpdateRelease release, CancellationToken cancellationToken = default)
        {
            PromptCount++;
            return Task.FromResult(Choice);
        }
    }

    private sealed class StubHttpHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string UserAgent { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
        }
    }
}
