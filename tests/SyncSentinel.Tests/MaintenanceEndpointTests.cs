using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// The wipe-and-quit endpoint that lets a portable user remove SyncSentinel's
/// footprint from Settings (no installer to do it). Verified through the real
/// endpoint with a fake <see cref="IAppMaintenance"/>; the actual deletion is
/// covered by <see cref="UninstallCleanerTests"/>.
/// </summary>
public sealed class MaintenanceEndpointTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-maint-" + Guid.NewGuid().ToString("N"));

    public MaintenanceEndpointTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
        {
            Directory.Delete(_scratch, recursive: true);
        }
    }

    private sealed class RecordingMaintenance : IAppMaintenance
    {
        public int Calls { get; private set; }
        public void WipeDataAndQuit() => Calls++;
    }

    [Fact]
    public async Task Post_wipe_invokes_maintenance_and_accepts()
    {
        var maintenance = new RecordingMaintenance();
        await using var app = await TestApp.StartAsync(
            Path.Combine(_scratch, "config"), s => s.AddSingleton<IAppMaintenance>(maintenance));

        var res = await app.GetTestClient().PostAsync("/api/maintenance/wipe", null);

        Assert.True(res.IsSuccessStatusCode, $"expected 2xx, got {(int)res.StatusCode}");
        Assert.Equal(1, maintenance.Calls);
    }
}
