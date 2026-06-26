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
        services.AddSingleton<RobocopyRunner>();
        services.AddSingleton<JobRunCoordinator>();
        services.AddSingleton<ConfigService>();
        // ConfigService depends on a ConfigStore — the shell registers one at
        // %APPDATA%; tests register one at a scratch dir.
    }

    /// <summary>Map SyncSentinel's endpoints (REST + SignalR) onto the app.</summary>
    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/ping", () => Results.Json(new { message = "pong" }));

        // ── Config (whole document for the UI to render) ──────────────────────
        app.MapGet("/api/config", (ConfigService cfg) => Results.Json(cfg.Current));

        // ── Jobs ──────────────────────────────────────────────────────────────
        app.MapPost("/api/jobs", (Job job, ConfigService cfg) => Results.Json(cfg.AddJob(job)));
        app.MapPut("/api/jobs/{id}", (string id, Job job, ConfigService cfg) =>
            cfg.UpdateJob(job with { Id = id }) ? Results.NoContent() : Results.NotFound());
        app.MapDelete("/api/jobs/{id}", (string id, ConfigService cfg) =>
            cfg.DeleteJob(id) ? Results.NoContent() : Results.NotFound());
        app.MapPost("/api/jobs/{id}/run", (string id, ConfigService cfg, JobRunCoordinator coordinator) =>
        {
            var resolved = cfg.ResolveJob(id);
            if (resolved is null) return Results.NotFound();
            return coordinator.TryStart(resolved)
                ? Results.Accepted()
                : Results.Conflict(new { message = "a run is already in progress" });
        });

        // ── Effective-command preview (works for unsaved edits) ───────────────
        app.MapPost("/api/preview", (Job job, ConfigService cfg) =>
        {
            var args = RobocopyCommand.Build(JobResolver.Resolve(job, cfg.Current));
            return Results.Json(new { command = RenderCommand(args), args });
        });

        // ── Folder / file exclusion sets ──────────────────────────────────────
        app.MapPost("/api/folder-sets", (FolderExclusionSet s, ConfigService cfg) => Results.Json(cfg.AddFolderSet(s)));
        app.MapPut("/api/folder-sets/{id}", (string id, FolderExclusionSet s, ConfigService cfg) =>
            cfg.UpdateFolderSet(s with { Id = id }) ? Results.NoContent() : Results.NotFound());
        app.MapDelete("/api/folder-sets/{id}", (string id, ConfigService cfg) =>
            cfg.DeleteFolderSet(id) ? Results.NoContent() : Results.NotFound());

        app.MapPost("/api/file-sets", (FileExclusionSet s, ConfigService cfg) => Results.Json(cfg.AddFileSet(s)));
        app.MapPut("/api/file-sets/{id}", (string id, FileExclusionSet s, ConfigService cfg) =>
            cfg.UpdateFileSet(s with { Id = id }) ? Results.NoContent() : Results.NotFound());
        app.MapDelete("/api/file-sets/{id}", (string id, ConfigService cfg) =>
            cfg.DeleteFileSet(id) ? Results.NoContent() : Results.NotFound());

        // ── Settings ──────────────────────────────────────────────────────────
        app.MapPut("/api/settings", (GlobalSettings s, ConfigService cfg) =>
        {
            cfg.UpdateSettings(s);
            return Results.NoContent();
        });

        app.MapHub<StatusHub>("/hubs/status");
    }

    // Render the robocopy arg tokens as a readable command line for the editor's
    // live preview (quote tokens containing spaces, e.g. paths).
    private static string RenderCommand(IReadOnlyList<string> args) =>
        "robocopy " + string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
}
