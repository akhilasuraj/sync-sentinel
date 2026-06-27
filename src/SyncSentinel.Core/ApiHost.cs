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
        // Storage lives under one root (StoragePaths) — the shell registers it at
        // %APPDATA%\SyncSentinel; tests register a scratch root.
        services.AddSingleton(sp => new ConfigStore(sp.GetRequiredService<StoragePaths>().Root));
        services.AddSingleton(sp => new RunHistoryStore(sp.GetRequiredService<StoragePaths>().HistoryDbPath));
        services.AddSingleton(sp => new RunRecorder(
            sp.GetRequiredService<RunHistoryStore>(),
            sp.GetRequiredService<ConfigService>(),
            sp.GetRequiredService<StoragePaths>()));
        services.AddSingleton<ConfigService>();
        services.AddSingleton<RunQueue>();
        services.AddSingleton<Scheduler>();
        // Default no-op; the shell overrides with the registry-backed AutostartManager.
        services.AddSingleton<IAutostart, NoOpAutostart>();
        // Default no-op; the shell overrides with the native-dialog FolderPicker.
        services.AddSingleton<IFolderPicker, NoOpFolderPicker>();
        services.AddHostedService<QueuePumpService>(); // drains the queue (incl. tests)
        // SchedulerTickService (auto-schedule due jobs) is registered by the shell
        // only, so tests don't get surprise scheduled runs.
    }

    /// <summary>Map SyncSentinel's endpoints (REST + SignalR) onto the app.</summary>
    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/ping", () => Results.Json(new { message = "pong" }));

        // ── Capabilities (shell-only features the UI conditionally enables) ───────
        app.MapGet("/api/capabilities", (IFolderPicker picker) =>
            Results.Json(new { folderPicker = picker.Available }));

        // ── Folder picker (native dialog via the shell seam) ──────────────────────
        app.MapPost("/api/pick-folder", async (PickFolderRequest req, IFolderPicker picker) =>
        {
            if (!picker.Available)
            {
                return Results.StatusCode(StatusCodes.Status501NotImplemented);
            }
            var path = await picker.PickFolderAsync(req.InitialPath, req.Title);
            return path is null ? Results.NoContent() : Results.Json(new { path });
        });

        // Existence check behind the editor's path hint. Folder-oriented, so a file
        // path reports false; the source/destination interpretation is the UI's.
        app.MapGet("/api/path-exists", (string path) =>
            Results.Json(new { exists = Directory.Exists(path) }));

        // ── Config (whole document for the UI to render) ──────────────────────
        app.MapGet("/api/config", (ConfigService cfg) => Results.Json(cfg.Current));

        // ── Jobs ──────────────────────────────────────────────────────────────
        app.MapPost("/api/jobs", (Job job, ConfigService cfg) => Results.Json(cfg.AddJob(job)));
        app.MapPut("/api/jobs/{id}", (string id, Job job, ConfigService cfg) =>
            cfg.UpdateJob(job with { Id = id }) ? Results.NoContent() : Results.NotFound());
        app.MapDelete("/api/jobs/{id}", (string id, ConfigService cfg) =>
            cfg.DeleteJob(id) ? Results.NoContent() : Results.NotFound());
        app.MapPost("/api/jobs/{id}/run", (string id, Scheduler scheduler) =>
            scheduler.RunNow(id) ? Results.Accepted() : Results.NotFound());

        // ── Run history ───────────────────────────────────────────────────────
        app.MapGet("/api/jobs/{id}/runs", (string id, RunHistoryStore history) =>
            Results.Json(history.ListByJob(id)));
        app.MapGet("/api/runs/{runId}/log", (string runId, RunHistoryStore history) =>
        {
            var run = history.Get(runId);
            if (run is null || !File.Exists(run.LogPath))
            {
                return Results.NotFound();
            }
            return Results.Text(File.ReadAllText(run.LogPath));
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
        app.MapPut("/api/settings", (GlobalSettings s, ConfigService cfg, IAutostart autostart) =>
        {
            cfg.UpdateSettings(s);
            // Apply the login-autostart preference immediately (best-effort: the
            // settings are already persisted; autostart is non-essential).
            try { autostart.Apply(s.Autostart); }
            catch { /* autostart is non-essential */ }
            return Results.NoContent();
        });

        app.MapHub<StatusHub>("/hubs/status");
    }

    // Render the robocopy arg tokens as a readable command line for the editor's
    // live preview (quote tokens containing spaces, e.g. paths).
    private static string RenderCommand(IReadOnlyList<string> args) =>
        "robocopy " + string.Join(' ', args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
}
