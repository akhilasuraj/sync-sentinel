using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace SyncSentinel.Core;

/// <summary>
/// Phase-0 liveness proof: pushes an incrementing <c>tick</c> to all clients
/// once a second (and immediately on start), so the UI can show the backend
/// is alive over SignalR. Later phases replace this with real job-status pushes.
/// </summary>
public sealed class HeartbeatService(IHubContext<StatusHub> hub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long n = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                await hub.Clients.All.SendAsync("tick", ++n, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // host shutting down — expected
        }
    }
}
