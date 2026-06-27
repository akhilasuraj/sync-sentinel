using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// The per-job run-state feed (GET /api/jobs/status) that backs the job card's
/// status dot and next-run countdown. Last-run status comes from the history
/// store (persists across restarts); next-due is derived from it via Schedule.
/// </summary>
public sealed class JobStatusEndpointTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-test-" + Guid.NewGuid().ToString("N"));

    public JobStatusEndpointTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    private sealed record CreatedJob(string Id);
    private sealed record JobStatusDto(string JobId, string? LastStatus, DateTimeOffset? NextDueUtc, string State);

    [Fact]
    public async Task Status_reports_each_jobs_most_recent_run_status()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();

        var create = await client.PostAsJsonAsync(
            "/api/jobs", new { name = "scratch", source = @"C:\src", destination = @"C:\dst" });
        create.EnsureSuccessStatusCode();
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();

        // Two recorded runs; the most recent (Error) is the one the card should show.
        var history = app.Services.GetRequiredService<RunHistoryStore>();
        history.Add(Run(job!.Id, "Success", DateTimeOffset.UtcNow.AddMinutes(-30)));
        history.Add(Run(job.Id, "Error", DateTimeOffset.UtcNow.AddMinutes(-5)));

        var statuses = await client.GetFromJsonAsync<List<JobStatusDto>>("/api/jobs/status");

        var status = Assert.Single(statuses!, s => s.JobId == job.Id);
        Assert.Equal("Error", status.LastStatus);
    }

    [Fact]
    public async Task Status_reports_next_due_as_last_finish_plus_interval_for_an_enabled_job()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();

        var create = await client.PostAsJsonAsync(
            "/api/jobs",
            new { name = "scratch", source = @"C:\src", destination = @"C:\dst", intervalMinutes = 15, enabled = true });
        create.EnsureSuccessStatusCode();
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();

        var finished = DateTimeOffset.UtcNow.AddMinutes(-5);
        app.Services.GetRequiredService<RunHistoryStore>().Add(Run(job!.Id, "Success", finished));

        var statuses = await client.GetFromJsonAsync<List<JobStatusDto>>("/api/jobs/status");

        var status = Assert.Single(statuses!, s => s.JobId == job.Id);
        Assert.Equal(finished.AddMinutes(15), status.NextDueUtc);
    }

    [Fact]
    public async Task Status_reports_due_now_for_an_enabled_job_that_never_ran()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();
        var create = await client.PostAsJsonAsync(
            "/api/jobs", new { name = "scratch", source = @"C:\src", destination = @"C:\dst", enabled = true });
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();
        // No runs seeded — this job has never run.

        var statuses = await client.GetFromJsonAsync<List<JobStatusDto>>("/api/jobs/status");

        var status = Assert.Single(statuses!, s => s.JobId == job!.Id);
        Assert.NotNull(status.NextDueUtc);
        Assert.True(
            status.NextDueUtc > DateTimeOffset.UtcNow.AddMinutes(-1),
            "a never-run enabled job should be due ~now, not a year-0001 timestamp");
    }

    [Fact]
    public async Task Status_reports_running_for_the_job_in_the_run_slot()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();
        var create = await client.PostAsJsonAsync(
            "/api/jobs", new { name = "scratch", source = @"C:\src", destination = @"C:\dst" });
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();

        var queue = app.Services.GetRequiredService<RunQueue>();
        queue.Enqueue(job!.Id);
        queue.Dequeue(); // takes it into the single running slot

        var statuses = await client.GetFromJsonAsync<List<JobStatusDto>>("/api/jobs/status");

        var status = Assert.Single(statuses!, s => s.JobId == job.Id);
        Assert.Equal("Running", status.State);
    }

    [Fact]
    public async Task Status_reports_queued_for_a_pending_job()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();
        var create = await client.PostAsJsonAsync(
            "/api/jobs", new { name = "scratch", source = @"C:\src", destination = @"C:\dst" });
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();

        app.Services.GetRequiredService<RunQueue>().Enqueue(job!.Id); // pending, not yet running

        var statuses = await client.GetFromJsonAsync<List<JobStatusDto>>("/api/jobs/status");

        var status = Assert.Single(statuses!, s => s.JobId == job.Id);
        Assert.Equal("Queued", status.State);
    }

    [Fact]
    public async Task Status_reports_no_next_due_for_a_paused_job()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();
        var create = await client.PostAsJsonAsync(
            "/api/jobs", new { name = "scratch", source = @"C:\src", destination = @"C:\dst", enabled = false });
        var job = await create.Content.ReadFromJsonAsync<CreatedJob>();
        app.Services.GetRequiredService<RunHistoryStore>().Add(Run(job!.Id, "Success", DateTimeOffset.UtcNow.AddMinutes(-5)));

        var statuses = await client.GetFromJsonAsync<List<JobStatusDto>>("/api/jobs/status");

        var status = Assert.Single(statuses!, s => s.JobId == job.Id);
        Assert.Null(status.NextDueUtc);
    }

    private static RunRecord Run(string jobId, string status, DateTimeOffset finished) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        JobId = jobId,
        JobName = "scratch",
        Status = status,
        StartedUtc = finished.AddSeconds(-10),
        FinishedUtc = finished,
    };
}
