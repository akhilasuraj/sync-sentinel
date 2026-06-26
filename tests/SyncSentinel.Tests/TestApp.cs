using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// Boots the real <see cref="ApiHost"/> wiring under an in-memory TestServer,
/// so tests exercise the same endpoints/hub the shell runs — no Kestrel,
/// no sockets, no ports. Each app gets an isolated config directory.
/// </summary>
internal static class TestApp
{
    public static async Task<WebApplication> StartAsync(string? configDir = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        ApiHost.ConfigureServices(builder.Services);
        builder.Services.AddSingleton(new StoragePaths(
            configDir ?? Path.Combine(Path.GetTempPath(), "ss-testapp-" + Guid.NewGuid().ToString("N"))));

        var app = builder.Build();
        ApiHost.MapEndpoints(app);

        await app.StartAsync();
        return app;
    }
}
