using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;

namespace SyncSentinel.Tests;

public class PingTests
{
    [Fact]
    public async Task Ping_returns_pong()
    {
        await using var app = await TestApp.StartAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/ping");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PingResponse>();
        Assert.Equal("pong", body!.Message);
    }

    private record PingResponse(string Message);
}
