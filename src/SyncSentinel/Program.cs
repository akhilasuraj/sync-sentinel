using System.Threading;
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
    private const string MutexName = @"Local\SyncSentinel.SingleInstance";
    private const string ShowEventName = @"Local\SyncSentinel.Show";

    [STAThread]
    private static int Main(string[] args)
    {
        var smoke = args.Contains("--smoke");

        // Single instance: a second launch signals the running one to surface its
        // window, then exits. (Skipped for the short-lived --smoke check.)
        Mutex? mutex = null;
        if (!smoke)
        {
            mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { /* ignore */ }
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
        try
        {
            var settings = app.Services.GetRequiredService<ConfigService>().Current.Settings;
            new AutostartManager(Environment.ProcessPath!).Apply(settings.Autostart);
        }
        catch { /* autostart is non-essential */ }

        ApplicationConfiguration.Initialize();
        var form = new MainForm(url, startHidden: args.Contains("--tray"));

        // Surface the window when a second instance signals us.
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (showEvent.WaitOne())
            {
                try { form.BeginInvoke(() => form.ShowExternally()); } catch { break; }
            }
        })
        { IsBackground = true };
        listener.Start();

        Application.Run(form);

        mutex?.Dispose();
        app.StopAsync().GetAwaiter().GetResult();
        return 0;
    }
}
