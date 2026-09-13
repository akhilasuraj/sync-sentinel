using System.Diagnostics;

namespace SyncSentinel.Tests;

public sealed class ReleaseVersionContractTests
{
    [Fact]
    public void Windows_version_resource_padding_does_not_change_release_identity()
    {
        var result = AssertVersion("0.6.1                                             ", "0.6.1");

        Assert.True(result.ExitCode == 0, result.Output);
    }

    [Fact]
    public void A_genuinely_different_artifact_version_is_rejected()
    {
        var result = AssertVersion("0.6.0", "0.6.1");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("0.6.0", result.Output);
        Assert.Contains("0.6.1", result.Output);
    }

    private static ValidationResult AssertVersion(string actualVersion, string expectedVersion)
    {
        var root = RepositoryPaths.Root;
        var script = Path.Combine(root, ".github", "scripts", "Assert-ReleaseVersion.ps1");
        var process = Process.Start(new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = root,
            ArgumentList =
            {
                "-NoProfile",
                "-File",
                script,
                "-ActualVersion",
                actualVersion,
                "-ExpectedVersion",
                expectedVersion,
                "-ArtifactName",
                "test artifact",
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, output);
    }

    private sealed record ValidationResult(int ExitCode, string Output);
}
