using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SyncSentinel.Core;

/// <summary>
/// Configures SyncSentinel's in-process HTTP + SignalR surface. Shared by the
/// WinForms shell (real Kestrel on loopback) and the tests (in-memory
/// TestServer) so both exercise identical wiring.
/// </summary>
public static class ApiHost
{
    /// <summary>Register SyncSentinel's services on the host builder.</summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddSignalR();
        services.AddHostedService<HeartbeatService>();
    }

    /// <summary>Map SyncSentinel's endpoints (REST + SignalR) onto the app.</summary>
    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/ping", () => Results.Json(new { message = "pong" }));
        app.MapHub<StatusHub>("/hubs/status");
    }
}
