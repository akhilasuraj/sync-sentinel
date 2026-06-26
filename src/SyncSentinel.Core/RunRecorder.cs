namespace SyncSentinel.Core;

/// <summary>
/// Persists a completed run: writes the full output to a per-job .log file,
/// parses the robocopy summary into counts, stores the record, and prunes the
/// history per the configured retention. Clock + id generator + logs dir are
/// injectable so the recording logic is testable.
/// </summary>
public sealed class RunRecorder
{
    private readonly RunHistoryStore _history;
    private readonly ConfigService _config;
    private readonly string _logsDir;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<string> _newId;

    public RunRecorder(RunHistoryStore history, ConfigService config, StoragePaths paths)
        : this(history, config, paths.LogsDir, () => DateTimeOffset.UtcNow, () => Guid.NewGuid().ToString("N")[..8])
    {
    }

    public RunRecorder(RunHistoryStore history, ConfigService config, string logsDir, Func<DateTimeOffset> now, Func<string> newId)
    {
        _history = history;
        _config = config;
        _logsDir = logsDir;
        _now = now;
        _newId = newId;
    }

    public RunRecord Record(
        string jobId, string jobName,
        DateTimeOffset started, DateTimeOffset finished,
        RobocopyStatus status, int exitCode,
        IReadOnlyList<string> lines)
    {
        var id = _newId();
        var logPath = Path.Combine(_logsDir, jobId, $"{finished:yyyyMMdd_HHmmss}_{id}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllLines(logPath, lines);

        var summary = RobocopySummary.Parse(lines);
        var record = new RunRecord
        {
            Id = id,
            JobId = jobId,
            JobName = jobName,
            Status = status.ToString(),
            StartedUtc = started,
            FinishedUtc = finished,
            FilesCopied = summary.FilesCopied,
            FilesSkipped = summary.FilesSkipped,
            FilesFailed = summary.FilesFailed,
            FilesExtra = summary.FilesExtra,
            ExitCode = exitCode,
            LogPath = logPath,
        };
        _history.Add(record);
        Prune();
        return record;
    }

    private void Prune()
    {
        var all = _history.All();
        var expired = Retention.SelectExpired(all, _config.Current.Settings.Retention, _now());
        if (expired.Count == 0)
        {
            return;
        }
        var expiredSet = expired.ToHashSet();
        foreach (var run in all.Where(r => expiredSet.Contains(r.Id)))
        {
            try
            {
                if (File.Exists(run.LogPath))
                {
                    File.Delete(run.LogPath);
                }
            }
            catch
            {
                // best-effort log cleanup; the row is still pruned below
            }
        }
        _history.Delete(expired);
    }
}
