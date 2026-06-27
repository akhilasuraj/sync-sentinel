using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// The settings endpoint should apply the autostart preference immediately (not
/// only at next launch). Verified through the real endpoint with a fake
/// <see cref="IAutostart"/> in place of the registry; the registry impl itself is
/// covered by <see cref="AutostartManagerTests"/>.
/// </summary>
public sealed class AutostartEndpointTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-autostart-" + Guid.NewGuid().ToString("N"));

    public AutostartEndpointTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    // Records the last applied value; null means Apply was never called.
    private sealed class RecordingAutostart : IAutostart
    {
        public bool? Applied { get; private set; }
        public void Apply(bool enabled) => Applied = enabled;
    }

    private Task<Microsoft.AspNetCore.Builder.WebApplication> StartAsync(IAutostart autostart) =>
        TestApp.StartAsync(
            Path.Combine(_scratch, "config"),
            s => s.AddSingleton(autostart));

    [Fact]
    public async Task Put_settings_with_autostart_enabled_applies_it_now()
    {
        var autostart = new RecordingAutostart();
        await using var app = await StartAsync(autostart);
        var client = app.GetTestClient();
        var config = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");

        await client.PutAsJsonAsync("/api/settings", config!.Settings with { Autostart = true });

        Assert.True(autostart.Applied);
    }

    [Fact]
    public async Task Put_settings_with_autostart_disabled_clears_it_now()
    {
        var autostart = new RecordingAutostart();
        await using var app = await StartAsync(autostart);
        var client = app.GetTestClient();
        var config = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");

        await client.PutAsJsonAsync("/api/settings", config!.Settings with { Autostart = false });

        Assert.False(autostart.Applied); // false, not null: Apply was called with false
    }
}
