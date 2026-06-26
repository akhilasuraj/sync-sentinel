namespace SyncSentinel.Core;

/// <summary>
/// A single completed run's metadata (the row stored in the history db). The
/// full robocopy output lives in the file at <see cref="LogPath"/>.
/// </summary>
public sealed record RunRecord
{
    public required string Id { get; init; }
    public required string JobId { get; init; }
    public required string JobName { get; init; }
    public required string Status { get; init; } // Success / Warning / Error
    public required DateTimeOffset StartedUtc { get; init; }
    public required DateTimeOffset FinishedUtc { get; init; }
    public int FilesCopied { get; init; }
    public int FilesSkipped { get; init; }
    public int FilesFailed { get; init; }
    public int FilesExtra { get; init; }
    public int ExitCode { get; init; }
    public string LogPath { get; init; } = "";

    public double DurationSeconds => (FinishedUtc - StartedUtc).TotalSeconds;
}
