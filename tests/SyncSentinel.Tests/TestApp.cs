using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// Boots the real <see cref="ApiHost"/> wiring under an in-memory TestServer,
/// so tests exercise the same endpoints/hub the shell runs — no Kestrel,
/// no sockets, no ports.
/// </summary>
internal static class TestApp
{
    public static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        ApiHost.ConfigureServices(builder.Services);
        var app = builder.Build();
        ApiHost.MapEndpoints(app);

        await app.StartAsync();
        return app;
    }
}
