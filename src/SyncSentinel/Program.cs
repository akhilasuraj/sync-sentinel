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
/// The thin shell: starts the in-process ASP.NET Core host (shared
/// <see cref="ApiHost"/> wiring) on a loopback ephemeral port, serves the
/// embedded React build, and hosts it in a WebView2 tray window.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Pin the content root to the exe folder so wwwroot resolves regardless
        // of the working directory the app is launched from (tray autostart).
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();
        // Loopback only, OS-assigned free port — never network-exposed.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        ApiHost.ConfigureServices(builder.Services);
        builder.Services.AddSingleton<IBackupJobSource, DemoJobSource>();

        var app = builder.Build();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        ApiHost.MapEndpoints(app);
        app.MapFallbackToFile("index.html");

        app.Start();
        var url = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        if (args.Contains("--smoke"))
        {
            var ok = SmokeCheck.Run(url).GetAwaiter().GetResult();
            app.StopAsync().GetAwaiter().GetResult();
            return ok ? 0 : 1;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(url));

        app.StopAsync().GetAwaiter().GetResult();
        return 0;
    }
}
