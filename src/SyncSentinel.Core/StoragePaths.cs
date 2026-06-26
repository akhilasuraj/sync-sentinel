namespace SyncSentinel.Core;

/// <summary>
/// Where SyncSentinel keeps its data, all under one root (%APPDATA%\SyncSentinel
/// for the app; a scratch dir in tests). The single place the storage layout is
/// defined — ConfigStore, RunHistoryStore and RunRecorder all derive from it.
/// </summary>
public sealed record StoragePaths(string Root)
{
    public string HistoryDbPath => Path.Combine(Root, "history.db");
    public string LogsDir => Path.Combine(Root, "logs");

    public static StoragePaths Default =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SyncSentinel"));
}
