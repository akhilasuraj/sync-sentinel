using System.Diagnostics;

namespace SyncSentinel.Tests;

public sealed class ReleaseFeedContractTests : IDisposable
{
    private static readonly string ValidSignature = Convert.ToBase64String(new byte[64]);
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

    [Fact]
    public void Historical_release_notes_link_must_target_that_exact_version()
    {
        var result = ValidateFeed(Appcast(
            Item("1.2.0", "<description>Changes in 1.2.0</description>"),
            Item("1.1.0", "<sparkle:releaseNotesLink>https://example.test/releases</sparkle:releaseNotesLink>")));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("incorrect release-notes link", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Historical_enclosure_metadata_must_match_its_release_version()
    {
        var historical = Item("1.1.0", "<description>Changes in 1.1.0</description>")
            .Replace("download/v1.1.0", "download/v9.9.9", StringComparison.Ordinal);
        var result = ValidateFeed(Appcast(
            Item("1.2.0", "<description>Changes in 1.2.0</description>"),
            historical));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("incorrect installer URL", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generation_from_a_prior_feed_retains_history_and_backfills_its_notes()
    {
        var feedDirectory = Path.Combine(_scratch, "feed");
        var notesDirectory = Path.Combine(_scratch, "notes");
        Directory.CreateDirectory(feedDirectory);
        Directory.CreateDirectory(notesDirectory);
        File.WriteAllText(Path.Combine(feedDirectory, "appcast.xml"), Appcast(Item("1.1.0", string.Empty)));
        File.WriteAllText(Path.Combine(notesDirectory, "1.1.0.md"), "Complete changes in 1.1.0");
        File.WriteAllText(Path.Combine(notesDirectory, "1.2.0.md"), "Changes in 1.2.0");

        var generator = Path.Combine(_scratch, "fake-generator.ps1");
        File.WriteAllText(generator, $$"""
            param([Parameter(ValueFromRemainingArguments = $true)] [string[]] $Arguments)
            if ('--reparse-existing' -notin $Arguments) { throw 'Expected --reparse-existing.' }
            $baseUrl = $Arguments[[Array]::IndexOf($Arguments, '--base-url') + 1]
            if ($baseUrl -ne 'https://example.test/releases/download/v1.2.0/') { throw "Unexpected base URL: $baseUrl" }
            $feedDirectory = $Arguments[[Array]::IndexOf($Arguments, '--appcast-output-directory') + 1]
            $appcastPath = Join-Path $feedDirectory 'appcast.xml'
            [xml] $feed = Get-Content $appcastPath -Raw
            $fragment = $feed.CreateDocumentFragment()
            $fragment.InnerXml = @'
            {{Item("1.2.0", "<description>Changes in 1.2.0</description>").Replace("<item>", "<item xmlns:sparkle=\"http://www.andymatuschak.org/xml-namespaces/sparkle\">")}}
            '@
            $feed.rss.channel.PrependChild($fragment.FirstChild) | Out-Null
            $feed.Save($appcastPath)
            [IO.File]::WriteAllText("$appcastPath.signature", '{{ValidSignature}}')
            """);

        var root = RepositoryPaths.Root;
        var script = Path.Combine(root, ".github", "scripts", "Build-ReleaseFeed.ps1");
        var result = RunPowerShell(
            root,
            script,
            $"-GeneratorPath \"{generator}\" -InstallerPath installer.exe -FeedDirectory \"{feedDirectory}\" " +
            $"-ChangeLogDirectory \"{notesDirectory}\" -Version 1.2.0 " +
            "-BaseUrl https://example.test/releases/download/v1.2.0/ " +
            "-LatestAppcastUrl https://example.test/releases/latest/download/appcast.xml " +
            "-RepositoryUrl https://example.test/");

        Assert.True(result.ExitCode == 0, result.Output);
        System.Xml.Linq.XNamespace sparkle = "http://www.andymatuschak.org/xml-namespaces/sparkle";
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(feedDirectory, "appcast.xml"));
        var items = document.Root!.Element("channel")!.Elements("item").ToArray();
        Assert.Equal(new[] { "1.2.0", "1.1.0" }, items.Select(item => item.Element(sparkle + "version")!.Value));
        Assert.Equal("Complete changes in 1.1.0", items[1].Element("description")!.Value);
        Assert.Null(items[1].Element(sparkle + "releaseNotesLink"));
    }

    private ValidationResult ValidateFeed(string appcast, string extraArguments = "")
    {
        var appcastPath = Path.Combine(_scratch, "appcast.xml");
        var signaturePath = appcastPath + ".signature";
        File.WriteAllText(appcastPath, appcast);
        File.WriteAllText(signaturePath, ValidSignature);
        var root = RepositoryPaths.Root;
        var script = Path.Combine(root, ".github", "scripts", "Validate-ReleaseFeed.ps1");
        return RunPowerShell(root, script,
            $"-AppcastPath \"{appcastPath}\" -ExpectedVersion 1.2.0 -ExpectedInstallerUrl https://example.test/releases/download/v1.2.0/SyncSentinel-Setup.exe -RepositoryUrl https://example.test {extraArguments}");
    }

    private static ValidationResult RunPowerShell(string workingDirectory, string script, string arguments)
    {
        var process = Process.Start(new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = workingDirectory,
            Arguments = $"-NoProfile -File \"{script}\" {arguments}",
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
          <enclosure url="https://example.test/releases/download/v{{version}}/SyncSentinel-Setup.exe" sparkle:version="{{version}}" sparkle:signature="{{ValidSignature}}" />
        </item>
        """;

    private sealed record ValidationResult(int ExitCode, string Output);
}
