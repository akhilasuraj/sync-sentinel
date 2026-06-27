using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public sealed class RunRecorderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ss-rec-" + Guid.NewGuid().ToString("N"));
    private readonly RunHistoryStore _history;
    private readonly ConfigService _config;
    private int _ids;
    private DateTimeOffset _now = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);

    public RunRecorderTests()
    {
        Directory.CreateDirectory(_dir);
        _history = new RunHistoryStore(Path.Combine(_dir, "history.db"));
        _config = new ConfigService(new ConfigStore(Path.Combine(_dir, "config")));
    }

    public void Dispose()
    {
        _history.Dispose(); // close the connection so the file can be deleted
        Directory.Delete(_dir, recursive: true);
    }

    private RunRecorder NewRecorder() =>
        new(_history, _config, Path.Combine(_dir, "logs"), () => _now, () => $"id{++_ids}");

    private static readonly string[] SampleOutput =
    [
        "   New File   readme.txt",
        "   Files :         4         4         0         0         0         1",
    ];

    [Fact]
    public void Record_writes_a_log_file_and_stores_a_row_with_parsed_counts()
    {
        var rec = NewRecorder();

        var record = rec.Record("job1", "PEMS", _now.AddSeconds(-3), _now, RobocopyStatus.Warning, 9, SampleOutput);

        Assert.True(File.Exists(record.LogPath), "the .log file should be written");
        Assert.Contains("readme.txt", File.ReadAllText(record.LogPath));

        var stored = Assert.Single(_history.ListByJob("job1"));
        Assert.Equal("Warning", stored.Status);
        Assert.Equal(9, stored.ExitCode);
        Assert.Equal(4, stored.FilesCopied);
        Assert.Equal(1, stored.FilesExtra);
    }

    [Fact]
    public void RecordSkipped_stores_a_Skipped_run_with_the_reason_in_its_log()
    {
        var rec = NewRecorder();

        var record = rec.RecordSkipped("job1", "PEMS", "Source folder not found: C:\\nope", _now);

        Assert.Equal("Skipped", record.Status);
        Assert.True(File.Exists(record.LogPath), "a log noting the skip reason should be written");
        Assert.Contains("Source folder not found", File.ReadAllText(record.LogPath));

        var stored = Assert.Single(_history.ListByJob("job1"));
        Assert.Equal("Skipped", stored.Status);
    }

    [Fact]
    public void Record_prunes_history_beyond_retention_and_deletes_old_log_files()
    {
        _config.UpdateSettings(_config.Current.Settings with { Retention = new RetentionSettings { RunsPerJob = 1, Days = 3650 } });
        var rec = NewRecorder();

        var first = rec.Record("job1", "PEMS", _now, _now, RobocopyStatus.Success, 1, SampleOutput);
        _now = _now.AddMinutes(1);
        var second = rec.Record("job1", "PEMS", _now, _now, RobocopyStatus.Success, 1, SampleOutput);

        // Only the newest run remains; the older run's row and log file are gone.
        var only = Assert.Single(_history.ListByJob("job1"));
        Assert.Equal(second.Id, only.Id);
        Assert.False(File.Exists(first.LogPath), "the pruned run's log file should be deleted");
    }
}
