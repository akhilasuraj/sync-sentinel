using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// The folder-picker HTTP surface: a capability flag, the pick endpoint, and a
/// path-existence check. Verified through the real endpoints with a fake
/// <see cref="IFolderPicker"/> in place of the native dialog (the dialog impl is
/// shell glue, covered by --smoke + a manual run).
/// </summary>
public sealed class FolderPickerEndpointTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-picker-" + Guid.NewGuid().ToString("N"));

    public FolderPickerEndpointTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public async Task Capabilities_reports_folderPicker_false_by_default()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();

        var caps = await client.GetFromJsonAsync<Capabilities>("/api/capabilities");

        Assert.False(caps!.FolderPicker);
    }

    [Fact]
    public async Task Capabilities_reports_folderPicker_true_when_a_picker_is_registered()
    {
        await using var app = await StartAsync(new FakePicker(available: true, result: null));
        var client = app.GetTestClient();

        var caps = await client.GetFromJsonAsync<Capabilities>("/api/capabilities");

        Assert.True(caps!.FolderPicker);
    }

    [Fact]
    public async Task Pick_folder_returns_the_selected_path()
    {
        await using var app = await StartAsync(new FakePicker(available: true, result: @"C:\dev\Chosen"));
        var client = app.GetTestClient();

        var res = await client.PostAsJsonAsync("/api/pick-folder", new { initialPath = @"C:\dev", title = "Select source folder" });

        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<PickResult>();
        Assert.Equal(@"C:\dev\Chosen", body!.Path);
    }

    private sealed record PickResult(string Path);

    [Fact]
    public async Task Pick_folder_returns_204_when_cancelled()
    {
        await using var app = await StartAsync(new FakePicker(available: true, result: null));
        var client = app.GetTestClient();

        var res = await client.PostAsJsonAsync(
            "/api/pick-folder", new { initialPath = (string?)null, title = (string?)null });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    [Fact]
    public async Task Pick_folder_returns_501_when_no_picker_is_available()
    {
        // A result is set so a missing availability guard would wrongly return 200.
        await using var app = await StartAsync(new FakePicker(available: false, result: @"C:\ignored"));
        var client = app.GetTestClient();

        var res = await client.PostAsJsonAsync(
            "/api/pick-folder", new { initialPath = (string?)null, title = (string?)null });

        Assert.Equal(HttpStatusCode.NotImplemented, res.StatusCode);
    }

    [Fact]
    public async Task Path_exists_is_true_for_an_existing_directory()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();

        var res = await client.GetFromJsonAsync<ExistsResult>(
            $"/api/path-exists?path={Uri.EscapeDataString(_scratch)}");

        Assert.True(res!.Exists);
    }

    [Fact]
    public async Task Path_exists_is_false_for_a_missing_directory()
    {
        await using var app = await TestApp.StartAsync(Path.Combine(_scratch, "config"));
        var client = app.GetTestClient();

        var missing = Path.Combine(_scratch, "does-not-exist");
        var res = await client.GetFromJsonAsync<ExistsResult>(
            $"/api/path-exists?path={Uri.EscapeDataString(missing)}");

        Assert.False(res!.Exists);
    }

    private sealed record ExistsResult(bool Exists);

    private Task<Microsoft.AspNetCore.Builder.WebApplication> StartAsync(IFolderPicker picker) =>
        TestApp.StartAsync(Path.Combine(_scratch, "config"), s => s.AddSingleton(picker));

    // Fake native picker: configurable availability + a canned result; records the
    // initial path it was asked to seed.
    private sealed class FakePicker(bool available, string? result) : IFolderPicker
    {
        public bool Available => available;
        public string? LastInitialPath { get; private set; }
        public Task<string?> PickFolderAsync(string? initialPath, string? title)
        {
            LastInitialPath = initialPath;
            return Task.FromResult(result);
        }
    }

    private sealed record Capabilities(bool FolderPicker);
}
