using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// The version feed (GET /api/version) the UI shows in the sidebar. The value is
/// DI-provided so the shell supplies the real, build-stamped version.
/// </summary>
public sealed class VersionEndpointTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-ver-" + Guid.NewGuid().ToString("N"));

    public VersionEndpointTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
        {
            Directory.Delete(_scratch, recursive: true);
        }
    }

    private sealed record VersionDto(string Version);

    [Fact]
    public async Task Returns_the_registered_app_version()
    {
        await using var app = await TestApp.StartAsync(
            Path.Combine(_scratch, "config"), s => s.AddSingleton(new AppVersion("1.2.3")));

        var dto = await app.GetTestClient().GetFromJsonAsync<VersionDto>("/api/version");

        Assert.Equal("1.2.3", dto!.Version);
    }

    [Fact]
    public async Task Defaults_to_the_dev_version_when_not_overridden()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));

        var dto = await app.GetTestClient().GetFromJsonAsync<VersionDto>("/api/version");

        Assert.Equal(AppVersion.Dev, dto!.Version);
    }
}
