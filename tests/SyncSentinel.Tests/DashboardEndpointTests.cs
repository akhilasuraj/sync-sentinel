using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// The dashboard's backend queries: the recent-activity feed across all jobs
/// (GET /api/runs/recent) and the rolling 7-day aggregate (GET /api/stats).
/// </summary>
public sealed class DashboardEndpointTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-dash-" + Guid.NewGuid().ToString("N"));

    public DashboardEndpointTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    private sealed record RunDto(string Id, string JobId, string Status);
    private sealed record StatsDto(int Runs, int FilesCopied, int Failures);

    private static RunRecord Run(string id, string jobId, DateTimeOffset finished, string status = "Success", int copied = 0) => new()
    {
        Id = id,
        JobId = jobId,
        JobName = "Job " + jobId,
        Status = status,
        StartedUtc = finished.AddSeconds(-1),
        FinishedUtc = finished,
        FilesCopied = copied,
    };

    [Fact]
    public async Task Recent_runs_endpoint_returns_the_newest_across_all_jobs()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var history = app.Services.GetRequiredService<RunHistoryStore>();
        var t0 = DateTimeOffset.UtcNow;
        history.Add(Run("a", "j1", t0.AddMinutes(-5)));
        history.Add(Run("b", "j2", t0.AddMinutes(-1)));

        var runs = await app.GetTestClient().GetFromJsonAsync<List<RunDto>>("/api/runs/recent");

        Assert.Equal(["b", "a"], runs!.Select(r => r.Id));
    }

    [Fact]
    public async Task Stats_endpoint_summarizes_the_last_7_days()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var history = app.Services.GetRequiredService<RunHistoryStore>();
        var now = DateTimeOffset.UtcNow;
        history.Add(Run("a", "j1", now.AddDays(-1), "Success", copied: 10));
        history.Add(Run("b", "j1", now.AddDays(-2), "Error"));
        history.Add(Run("old", "j1", now.AddDays(-10), "Success", copied: 999)); // outside the window

        var stats = await app.GetTestClient().GetFromJsonAsync<StatsDto>("/api/stats");

        Assert.Equal(2, stats!.Runs);
        Assert.Equal(10, stats.FilesCopied);
        Assert.Equal(1, stats.Failures);
    }
}
