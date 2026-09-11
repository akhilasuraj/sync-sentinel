using System.Diagnostics;

namespace SyncSentinel.Tests;

public sealed class ReleaseFeedContractTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-release-feed-" + Guid.NewGuid().ToString("N"));

    public ReleaseFeedContractTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public void Cumulative_feed_with_release_notes_satisfies_the_release_contract()
    {
        var result = ValidateFeed(Appcast(
            Item("1.2.0", "<description>All changes in 1.2.0</description>"),
            Item("1.1.0", "<sparkle:releaseNotesLink>https://example.test/releases/tag/v1.1.0</sparkle:releaseNotesLink>")));

        Assert.True(result.ExitCode == 0, result.Output);
    }

    [Fact]
    public void Feed_without_notes_for_an_applicable_release_is_rejected()
    {
        var result = ValidateFeed(Appcast(Item("1.2.0", string.Empty)));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("release notes", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Feed_that_drops_a_previously_published_version_is_rejected()
    {
        var result = ValidateFeed(
            Appcast(Item("1.2.0", "<description>All changes in 1.2.0</description>")),
            "-RequiredVersions 1.1.0");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("was not retained", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Feed_versions_must_be_ordered_newest_first()
    {
        var result = ValidateFeed(Appcast(
            Item("1.1.0", "<description>Changes in 1.1.0</description>"),
            Item("1.2.0", "<description>Changes in 1.2.0</description>")));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("newest first", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    private ValidationResult ValidateFeed(string appcast, string extraArguments = "")
    {
        var appcastPath = Path.Combine(_scratch, "appcast.xml");
        var signaturePath = appcastPath + ".signature";
        File.WriteAllText(appcastPath, appcast);
        File.WriteAllText(signaturePath, "test-signature");
        var root = FindRepositoryRoot();
        var script = Path.Combine(root, ".github", "scripts", "Validate-ReleaseFeed.ps1");
        var process = Process.Start(new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = root,
            Arguments = $"-NoProfile -File \"{script}\" -AppcastPath \"{appcastPath}\" -ExpectedVersion 1.2.0 -ExpectedInstallerUrl https://example.test/v1.2.0/SyncSentinel-Setup.exe {extraArguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, output);
    }

    private static string Appcast(params string[] items) => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0" xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle">
          <channel>
            <title>SyncSentinel</title>
            {{string.Join(Environment.NewLine, items)}}
          </channel>
        </rss>
        """;

    private static string Item(string version, string notes) => $$"""
        <item>
          <title>SyncSentinel {{version}}</title>
          {{notes}}
          <sparkle:version>{{version}}</sparkle:version>
          <enclosure url="https://example.test/v{{version}}/SyncSentinel-Setup.exe" sparkle:version="{{version}}" sparkle:signature="signed" />
        </item>
        """;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SyncSentinel.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }

    private sealed record ValidationResult(int ExitCode, string Output);
}
