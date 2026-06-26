using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;

namespace SyncSentinel.Tests;

public sealed class RunEndpointTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-test-" + Guid.NewGuid().ToString("N"));

    public RunEndpointTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    private sealed record CreatedJob(string Id);

    [Fact]
    public async Task Running_a_configured_job_mirrors_it_and_broadcasts_runFinished()
    {
        var src = Path.Combine(_scratch, "src");
        var dst = Path.Combine(_scratch, "dst");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "hi");

        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var server = app.GetTestServer();
        var client = app.GetTestClient();

        // Create the job through the API; the server assigns its id.
        var create = await client.PostAsJsonAsync("/api/jobs", new { name = "scratch", source = src, destination = dst });
        create.EnsureSuccessStatusCode();
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();
        Assert.False(string.IsNullOrEmpty(job!.Id));

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/status"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();
        var finished = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<string, int>("runFinished", (status, _) => finished.TrySetResult(status));
        await connection.StartAsync();

        var run = await client.PostAsync($"/api/jobs/{job.Id}/run", null);
        run.EnsureSuccessStatusCode();

        var winner = await Task.WhenAny(finished.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(winner == finished.Task, "expected a runFinished broadcast within 10s");
        Assert.Equal("Success", finished.Task.Result);
        Assert.True(File.Exists(Path.Combine(dst, "a.txt")), "the job should have mirrored a.txt");

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Running_an_unknown_job_returns_404()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));

        var run = await app.GetTestClient().PostAsync("/api/jobs/does-not-exist/run", null);

        Assert.Equal(HttpStatusCode.NotFound, run.StatusCode);
    }
}
