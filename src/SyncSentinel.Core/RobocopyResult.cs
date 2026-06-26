namespace SyncSentinel.Core;

/// <summary>Outcome category for a robocopy run (see CONTEXT.md run status).</summary>
public enum RobocopyStatus
{
    Success,
    Warning,
    Error,
}

/// <summary>
/// The outcome of a robocopy run. Robocopy uses bitmapped exit codes: 0-7 are
/// success, bit 8 ("some files could not be copied", e.g. locked) is a non-fatal
/// warning, bit 16 is a fatal error.
/// </summary>
public sealed record RobocopyResult(int ExitCode, RobocopyStatus Status)
{
    public static RobocopyResult FromExitCode(int exitCode)
    {
        var status =
            (exitCode & 16) != 0 ? RobocopyStatus.Error
            : (exitCode & 8) != 0 ? RobocopyStatus.Warning
            : RobocopyStatus.Success;
        return new RobocopyResult(exitCode, status);
    }
}
