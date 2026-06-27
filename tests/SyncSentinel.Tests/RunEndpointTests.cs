using System.Net;
using System.Net.Http.Json;
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

    private sealed record CreatedJob(string Id);
    private sealed record ErrorDto(string Error);

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
        Assert.Equal("Success", await finished.Task);
        Assert.True(File.Exists(Path.Combine(dst, "a.txt")), "the job should have mirrored a.txt");

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task A_scheduled_run_whose_source_went_missing_is_recorded_as_Skipped()
    {
        var src = Path.Combine(_scratch, "src");
        var dst = Path.Combine(_scratch, "dst");
        Directory.CreateDirectory(src);

        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();
        var create = await client.PostAsJsonAsync(
            "/api/jobs", new { name = "scratch", source = src, destination = dst, enabled = true });
        create.EnsureSuccessStatusCode();
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();

        // The source disappears after the job was created and enabled.
        Directory.Delete(src, recursive: true);

        var scheduler = app.Services.GetRequiredService<Scheduler>();
        scheduler.Tick();            // a never-run enabled job is due immediately
        await scheduler.PumpAsync(); // drains it through the coordinator

        var run = Assert.Single(app.Services.GetRequiredService<RunHistoryStore>().ListByJob(job!.Id));
        Assert.Equal("Skipped", run.Status);
        Assert.False(Directory.Exists(dst), "a skipped run must not invoke robocopy (no destination created)");
    }

    [Fact]
    public async Task Run_now_is_blocked_with_a_reason_when_the_source_is_missing()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();
        var create = await client.PostAsJsonAsync(
            "/api/jobs",
            new { name = "scratch", source = Path.Combine(_scratch, "missing-src"), destination = Path.Combine(_scratch, "dst"), enabled = false });
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();

        var run = await client.PostAsync($"/api/jobs/{job!.Id}/run", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, run.StatusCode);
        var body = await run.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.Contains("Source", body!.Error);
    }

    [Fact]
    public async Task Enabling_a_job_whose_source_is_missing_is_rejected_with_a_reason()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();
        var missingSrc = Path.Combine(_scratch, "missing-src");
        var dst = Path.Combine(_scratch, "dst");
        var create = await client.PostAsJsonAsync(
            "/api/jobs", new { name = "scratch", source = missingSrc, destination = dst, enabled = false });
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();

        var put = await client.PutAsJsonAsync(
            $"/api/jobs/{job!.Id}", new { name = "scratch", source = missingSrc, destination = dst, enabled = true });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, put.StatusCode);
        var body = await put.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.Contains("Source", body!.Error);
    }

    [Fact]
    public async Task Editing_an_already_enabled_job_with_a_now_missing_source_still_saves()
    {
        var src = Path.Combine(_scratch, "src");
        var dst = Path.Combine(_scratch, "dst");
        Directory.CreateDirectory(src);
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();
        var create = await client.PostAsJsonAsync(
            "/api/jobs", new { name = "scratch", source = src, destination = dst, enabled = true });
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();

        Directory.Delete(src, recursive: true); // source vanishes after it was enabled

        // Editing an already-enabled job must not be blocked (no enable transition).
        var put = await client.PutAsJsonAsync(
            $"/api/jobs/{job!.Id}", new { name = "renamed", source = src, destination = dst, enabled = true });

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
    }

    [Fact]
    public async Task Running_an_unknown_job_returns_404()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));

        var run = await app.GetTestClient().PostAsync("/api/jobs/does-not-exist/run", null);

        Assert.Equal(HttpStatusCode.NotFound, run.StatusCode);
    }
}
