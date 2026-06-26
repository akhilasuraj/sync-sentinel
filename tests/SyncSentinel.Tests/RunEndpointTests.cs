using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class RunEndpointTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-test-" + Guid.NewGuid().ToString("N"));

    public RunEndpointTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    private sealed class FixedJobSource(BackupJob job) : IBackupJobSource
    {
        public BackupJob GetCurrent() => job;
    }

    [Fact]
    public async Task Posting_run_mirrors_the_job_and_broadcasts_runFinished()
    {
        var src = Path.Combine(_scratch, "src");
        var dst = Path.Combine(_scratch, "dst");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "hi");
        var job = new BackupJob { Name = "scratch", Source = src, Destination = dst };

        await using var app = await TestApp.StartAsync(s =>
            s.AddSingleton<IBackupJobSource>(new FixedJobSource(job)));
        var server = app.GetTestServer();

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

        var response = await app.GetTestClient().PostAsync("/api/run", null);
        response.EnsureSuccessStatusCode();

        var winner = await Task.WhenAny(finished.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(winner == finished.Task, "expected a runFinished broadcast within 10s");
        Assert.Equal("Success", finished.Task.Result);
        Assert.True(File.Exists(Path.Combine(dst, "a.txt")), "the job should have mirrored a.txt");

        await connection.DisposeAsync();
    }
}
