using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyncSentinel.Core;

namespace SyncSentinel;

/// <summary>
/// The thin shell: enforces a single instance, starts the in-process ASP.NET Core
/// host (shared <see cref="ApiHost"/> wiring) on a loopback ephemeral port, serves
/// the embedded React build, hosts it in a WebView2 tray window, and reconciles
/// login autostart with the saved preference.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var smoke = args.Contains("--smoke");
        var quit = args.Contains("--quit");

        // --uninstall: remove the app's footprint (HKCU Run entry always; the data
        // root when --purge-data) and exit. Invoked by the installer's uninstaller
        // before it deletes files; no mutex, host, or window. The installer runs
        // --quit first, so nothing holds the data locked.
        if (args.Contains("--uninstall"))
        {
            new UninstallCleaner(StoragePaths.Default, new AutostartManager(Environment.ProcessPath!))
                .Clean(purgeData: args.Contains("--purge-data"));
            return 0;
        }

        // Single instance: a second launch signals the running one to surface its
        // window (or, under --quit, to exit), then exits itself. (Skipped for the
        // short-lived --smoke check.)
        SingleInstanceGuard? guard = null;
        if (!smoke)
        {
            guard = new SingleInstanceGuard();
            if (!guard.TryAcquire())
            {
                // Another instance owns the slot: ask it to surface (or quit), then exit.
                guard.Signal(quit ? InstanceSignal.Quit : InstanceSignal.Show);
                guard.Dispose();
                return 0;
            }

            // We are the only instance, so --quit has nothing to signal — just exit.
            if (quit)
            {
                guard.Dispose();
                return 0;
            }
        }

        // Pin the content root to the exe folder so wwwroot resolves regardless of
        // the working directory the app is launched from (tray autostart).
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); // loopback only, OS-assigned port
        ApiHost.ConfigureServices(builder.Services);

        // Surface the real, build-stamped version in the UI (overrides the dev
        // default registered by ApiHost). Strip any SourceLink "+metadata" suffix.
        var stamped = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = string.IsNullOrWhiteSpace(stamped) ? AppVersion.Dev : stamped.Split('+')[0];
        builder.Services.AddSingleton(new AppVersion(version));

        var paths = smoke
            ? new StoragePaths(Path.Combine(Path.GetTempPath(), "SyncSentinelSmoke"))
            : StoragePaths.Default;
        if (smoke && Directory.Exists(paths.Root))
        {
            Directory.Delete(paths.Root, recursive: true); // fresh seed each smoke run
        }
        builder.Services.AddSingleton(paths);
        // Auto-scheduling runs only in the real app (tests drive the scheduler
        // directly), so register the periodic tick here, not in ApiHost.
        builder.Services.AddHostedService<SchedulerTickService>();
        // Override the no-op IAutostart with the registry-backed impl for real
        // runs only; --smoke keeps the no-op so it can never touch the Run key.
        // Likewise the native folder picker — its UI context is wired once the
        // MainForm exists (below); --smoke keeps the no-op (no window).
        FolderPicker? folderPicker = null;
        ShellAppMaintenance? maintenance = null;
        if (!smoke)
        {
            builder.Services.AddSingleton<IAutostart>(new AutostartManager(Environment.ProcessPath!));
            folderPicker = new FolderPicker();
            builder.Services.AddSingleton<IFolderPicker>(folderPicker);
            maintenance = new ShellAppMaintenance(Environment.ProcessPath!);
            builder.Services.AddSingleton<IAppMaintenance>(maintenance);
        }

        var app = builder.Build();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        ApiHost.MapEndpoints(app);
        app.MapFallbackToFile("index.html");

        app.Start();
        var url = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        if (smoke)
        {
            var ok = SmokeCheck.Run(url).GetAwaiter().GetResult();
            app.StopAsync().GetAwaiter().GetResult();
            return ok ? 0 : 1;
        }

        // Reconcile login autostart with the saved preference (best-effort).
        // Reuses the DI-registered IAutostart so first launch / hand-edited config /
        // exe-path drift are repaired even when the live toggle wasn't used.
        try
        {
            var settings = app.Services.GetRequiredService<ConfigService>().Current.Settings;
            app.Services.GetRequiredService<IAutostart>().Apply(settings.Autostart);
        }
        catch { /* autostart is non-essential */ }

        ApplicationConfiguration.Initialize();
        var form = new MainForm(url, startHidden: args.Contains("--tray"));

        // The folder picker marshals its native dialog onto the window; the DI
        // container was built before the form, so wire the UI context now.
        folderPicker?.SetUiContext(form);
        // Likewise the maintenance seam needs the window to quit after wiping.
        maintenance?.SetForm(form);

        // React to a second launch: --quit asks us to exit, otherwise surface the
        // window. (The installer/uninstaller use --quit to stop us cleanly before
        // touching files, since closing the window only hides to tray.) The guard
        // owns the mutex + named-event protocol; we supply the window actions.
        guard?.Listen(
            onShow: () => form.BeginInvoke(() => form.ShowExternally()),
            onQuit: () => form.BeginInvoke(() => form.ExitApplication()));

        Application.Run(form);

        guard?.Dispose();
        app.StopAsync().GetAwaiter().GetResult();
        return 0;
    }
}
