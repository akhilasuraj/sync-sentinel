namespace SyncSentinel.Core;

/// <summary>
/// The pre-run gate for a job: a run needs an existing <em>source</em> folder and
/// an existing destination <em>drive/root</em> (robocopy creates a missing
/// destination folder itself, but not a missing drive). Returns <c>null</c> when
/// the job may run, or a human-readable reason it may not. Used to block manual
/// Enable / Run-now (4xx + reason) and to skip scheduled runs (recorded as a
/// <c>Skipped</c> run rather than a failing robocopy).
/// </summary>
public static class RunPreconditions
{
    public static string? Check(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return $"Source folder not found: {source}";
        }
        var root = Path.GetPathRoot(destination);
        if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
        {
            return $"Destination drive not found: {root}";
        }
        return null;
    }
}
