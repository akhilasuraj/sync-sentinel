using System.Diagnostics;

namespace SyncSentinel.Tests;

public sealed class InstalledReleaseContractTests : IDisposable
{
    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "ss-installed-release-" + Guid.NewGuid().ToString("N"));

    public InstalledReleaseContractTests() => Directory.CreateDirectory(_scratch);

    public void Dispose() => Directory.Delete(_scratch, recursive: true);

    [Fact]
    public void Release_build_exposes_the_public_version_without_source_revision_metadata()
    {
        var root = RepositoryPaths.Root;
        var result = Process.Start(new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            Arguments = $"build src/SyncSentinel -c Release --no-restore -p:Version=9.8.7 -p:SkipWebBuild=true -o \"{_scratch}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;

        var standardOutput = result.StandardOutput.ReadToEnd();
        var standardError = result.StandardError.ReadToEnd();
        result.WaitForExit();

        Assert.True(result.ExitCode == 0, standardOutput + Environment.NewLine + standardError);
        var executable = Path.Combine(_scratch, "SyncSentinel.exe");
        var metadata = FileVersionInfo.GetVersionInfo(executable);
        Assert.Equal("9.8.7", metadata.ProductVersion);
        Assert.Equal("9.8.7.0", metadata.FileVersion);
    }

}
