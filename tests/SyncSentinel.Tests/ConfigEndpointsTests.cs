using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// API-contract tests for the config CRUD surface, exercised through the real
/// endpoints over an in-memory TestServer (each app gets an isolated config dir).
/// </summary>
public sealed class ConfigEndpointsTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-api-" + Guid.NewGuid().ToString("N"));

    public ConfigEndpointsTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    private Task<Microsoft.AspNetCore.Builder.WebApplication> StartAsync() =>
        TestApp.StartAsync(Path.Combine(_scratch, "config"));

    [Fact]
    public async Task Get_config_returns_the_seeded_document()
    {
        await using var app = await StartAsync();

        var config = await app.GetTestClient().GetFromJsonAsync<SyncSentinelConfig>("/api/config");

        Assert.NotNull(config);
        Assert.Contains(config!.FolderSets, s => s.Name == "Developer Defaults");
    }

    [Fact]
    public async Task Post_job_persists_it_and_assigns_an_id()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        var created = await (await client.PostAsJsonAsync("/api/jobs",
            new { name = "PEMS", source = @"C:\dev\PEMS", destination = @"D:\bak\PEMS" }))
            .Content.ReadFromJsonAsync<Job>();

        Assert.False(string.IsNullOrEmpty(created!.Id));
        var config = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");
        Assert.Contains(config!.Jobs, j => j.Id == created.Id && j.Name == "PEMS");
    }

    [Fact]
    public async Task Put_job_updates_existing_and_404s_unknown()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var job = await (await client.PostAsJsonAsync("/api/jobs",
            new { name = "A", source = "s", destination = "d" })).Content.ReadFromJsonAsync<Job>();

        var ok = await client.PutAsJsonAsync($"/api/jobs/{job!.Id}", job with { Name = "Renamed" });
        var missing = await client.PutAsJsonAsync("/api/jobs/ghost", job with { Name = "X" });

        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var config = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");
        Assert.Contains(config!.Jobs, j => j.Id == job.Id && j.Name == "Renamed");
    }

    [Fact]
    public async Task Delete_job_removes_it_and_404s_unknown()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var job = await (await client.PostAsJsonAsync("/api/jobs",
            new { name = "A", source = "s", destination = "d" })).Content.ReadFromJsonAsync<Job>();

        var ok = await client.DeleteAsync($"/api/jobs/{job!.Id}");
        var missing = await client.DeleteAsync("/api/jobs/ghost");

        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var config = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");
        Assert.DoesNotContain(config!.Jobs, j => j.Id == job.Id);
    }

    [Fact]
    public async Task Preview_returns_the_effective_command_resolved_against_current_sets()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // References the seeded DeveloperDefaults set -> its folders become /XD.
        var preview = await (await client.PostAsJsonAsync("/api/preview", new
        {
            name = "P",
            source = @"C:\dev\X",
            destination = @"D:\bak\X",
            folderSetIds = new[] { DefaultConfig.DeveloperDefaultsId },
        })).Content.ReadFromJsonAsync<PreviewResponse>();

        Assert.Contains("robocopy", preview!.Command);
        Assert.Contains(@"C:\dev\X", preview.Command);
        Assert.Contains("/XD", preview.Command);
        Assert.Contains("node_modules", preview.Command);
    }

    [Fact]
    public async Task Folder_set_crud_round_trips_through_the_api()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        var created = await (await client.PostAsJsonAsync("/api/folder-sets",
            new { name = "Custom", folders = new[] { "bin", "obj" } }))
            .Content.ReadFromJsonAsync<FolderExclusionSet>();
        Assert.False(string.IsNullOrEmpty(created!.Id));

        await client.PutAsJsonAsync($"/api/folder-sets/{created.Id}", created with { Folders = ["bin"] });
        var afterUpdate = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");
        Assert.Equal(["bin"], afterUpdate!.FolderSets.First(s => s.Id == created.Id).Folders);

        await client.DeleteAsync($"/api/folder-sets/{created.Id}");
        var afterDelete = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");
        Assert.DoesNotContain(afterDelete!.FolderSets, s => s.Id == created.Id);
    }

    [Fact]
    public async Task Post_file_set_persists_it()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        var created = await (await client.PostAsJsonAsync("/api/file-sets",
            new { name = "Binaries", patterns = new[] { "*.dll", "*.pdb" } }))
            .Content.ReadFromJsonAsync<FileExclusionSet>();

        var config = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");
        Assert.Contains(config!.FileSets, s => s.Id == created!.Id && s.Patterns.Contains("*.dll"));
    }

    [Fact]
    public async Task Put_settings_persists_changes()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        var config = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");

        var res = await client.PutAsJsonAsync("/api/settings", config!.Settings with { DefaultFlags = "/MIR /Z" });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var after = await client.GetFromJsonAsync<SyncSentinelConfig>("/api/config");
        Assert.Equal("/MIR /Z", after!.Settings.DefaultFlags);
    }

    private sealed record PreviewResponse(string Command);
}
