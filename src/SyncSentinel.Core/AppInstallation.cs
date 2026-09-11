using System.Text.Json.Serialization;

namespace SyncSentinel.Core;

[JsonConverter(typeof(JsonStringEnumConverter<AppDistribution>))]
public enum AppDistribution
{
    Portable,
    Installed,
}

public static class AppInstallation
{
    public static AppDistribution Detect(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var applicationDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath))
            ?? throw new ArgumentException("The executable path must include a directory.", nameof(executablePath));

        return File.Exists(Path.Combine(applicationDirectory, "unins000.exe"))
            ? AppDistribution.Installed
            : AppDistribution.Portable;
    }
}
