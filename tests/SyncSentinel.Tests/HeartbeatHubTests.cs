using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;

namespace SyncSentinel.Tests;

public class HeartbeatHubTests
{
    [Fact]
    public async Task Hub_pushes_a_tick_within_two_seconds()
    {
        await using var app = await TestApp.StartAsync();
        var server = app.GetTestServer();

        // SignalR client over the in-memory TestServer (long-polling needs only
        // the test message handler — no sockets/ports).
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/status"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        var tick = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<long>("tick", n => tick.TrySetResult(n));

        await connection.StartAsync();
        var winner = await Task.WhenAny(tick.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.True(winner == tick.Task, "expected a 'tick' from the heartbeat within 2s");
        Assert.True(await tick.Task >= 1);

        await connection.DisposeAsync();
    }
}
