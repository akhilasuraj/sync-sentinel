using Microsoft.Data.Sqlite;

namespace SyncSentinel.Core;

/// <summary>
/// Durable run history in a SQLite file (the app uses <see cref="DefaultPath"/> =
/// %APPDATA%\SyncSentinel\history.db; tests pass a scratch path). Stores one row
/// per completed run; the full output lives in the run's .log file.
/// </summary>
public sealed class RunHistoryStore
{
    private readonly string _connectionString;

    public RunHistoryStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        // Pooling off so a connection fully releases the file on dispose — lets
        // tests delete their scratch db, and avoids a stray lock on the live db.
        _connectionString = $"Data Source={dbPath};Pooling=False";
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS runs (
                id          TEXT PRIMARY KEY,
                jobId       TEXT NOT NULL,
                jobName     TEXT NOT NULL,
                status      TEXT NOT NULL,
                startedUtc  TEXT NOT NULL,
                finishedUtc TEXT NOT NULL,
                filesCopied INTEGER NOT NULL,
                filesSkipped INTEGER NOT NULL,
                filesFailed INTEGER NOT NULL,
                filesExtra  INTEGER NOT NULL,
                exitCode    INTEGER NOT NULL,
                logPath     TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_runs_job ON runs (jobId, finishedUtc DESC);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Add(RunRecord r)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO runs (id, jobId, jobName, status, startedUtc, finishedUtc,
                              filesCopied, filesSkipped, filesFailed, filesExtra, exitCode, logPath)
            VALUES ($id, $jobId, $jobName, $status, $startedUtc, $finishedUtc,
                    $copied, $skipped, $failed, $extra, $exit, $logPath);
            """;
        cmd.Parameters.AddWithValue("$id", r.Id);
        cmd.Parameters.AddWithValue("$jobId", r.JobId);
        cmd.Parameters.AddWithValue("$jobName", r.JobName);
        cmd.Parameters.AddWithValue("$status", r.Status);
        cmd.Parameters.AddWithValue("$startedUtc", r.StartedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$finishedUtc", r.FinishedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$copied", r.FilesCopied);
        cmd.Parameters.AddWithValue("$skipped", r.FilesSkipped);
        cmd.Parameters.AddWithValue("$failed", r.FilesFailed);
        cmd.Parameters.AddWithValue("$extra", r.FilesExtra);
        cmd.Parameters.AddWithValue("$exit", r.ExitCode);
        cmd.Parameters.AddWithValue("$logPath", r.LogPath);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<RunRecord> ListByJob(string jobId, int limit = 100)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM runs WHERE jobId = $jobId ORDER BY finishedUtc DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$jobId", jobId);
        cmd.Parameters.AddWithValue("$limit", limit);
        return Read(cmd);
    }

    public RunRecord? Get(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM runs WHERE id = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", id);
        return Read(cmd).FirstOrDefault();
    }

    public IReadOnlyList<RunRecord> All()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM runs ORDER BY finishedUtc DESC;";
        return Read(cmd);
    }

    public void Delete(IEnumerable<string> ids)
    {
        var list = ids.ToList();
        if (list.Count == 0)
        {
            return;
        }
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        foreach (var id in list)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM runs WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static IReadOnlyList<RunRecord> Read(SqliteCommand cmd)
    {
        var results = new List<RunRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new RunRecord
            {
                Id = reader.GetString(reader.GetOrdinal("id")),
                JobId = reader.GetString(reader.GetOrdinal("jobId")),
                JobName = reader.GetString(reader.GetOrdinal("jobName")),
                Status = reader.GetString(reader.GetOrdinal("status")),
                StartedUtc = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("startedUtc"))),
                FinishedUtc = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("finishedUtc"))),
                FilesCopied = reader.GetInt32(reader.GetOrdinal("filesCopied")),
                FilesSkipped = reader.GetInt32(reader.GetOrdinal("filesSkipped")),
                FilesFailed = reader.GetInt32(reader.GetOrdinal("filesFailed")),
                FilesExtra = reader.GetInt32(reader.GetOrdinal("filesExtra")),
                ExitCode = reader.GetInt32(reader.GetOrdinal("exitCode")),
                LogPath = reader.GetString(reader.GetOrdinal("logPath")),
            });
        }
        return results;
    }
}
