using System.Text.Json;

namespace SyncSentinel.Core;

/// <summary>
/// Loads and saves the <see cref="SyncSentinelConfig"/> as human-readable
/// config.json under a directory (<see cref="StoragePaths.Root"/> — %APPDATA%\
/// SyncSentinel for the app, a scratch dir in tests). On first run — when no
/// config.json exists — it writes the <see cref="DefaultConfig"/> seed and
/// returns it.
/// </summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;

    public ConfigStore(string directory) => _path = Path.Combine(directory, "config.json");

    public SyncSentinelConfig Load()
    {
        if (File.Exists(_path))
        {
            var config = JsonSerializer.Deserialize<SyncSentinelConfig>(File.ReadAllText(_path), Options);
            if (config is not null)
            {
                return config;
            }
        }

        var seed = DefaultConfig.Seed();
        Save(seed);
        return seed;
    }

    public void Save(SyncSentinelConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(config, Options));
    }
}
