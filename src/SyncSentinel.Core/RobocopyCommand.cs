namespace SyncSentinel.Core;

/// <summary>
/// Composes the robocopy argument list for a <see cref="BackupJob"/> — the
/// "effective command" (see CONTEXT.md). Returns a token list (not a string) so
/// callers pass it via ProcessStartInfo.ArgumentList and never hand-quote paths.
/// </summary>
public static class RobocopyCommand
{
    public static IReadOnlyList<string> Build(BackupJob job)
    {
        var args = new List<string> { job.Source, job.Destination };
        args.AddRange(job.Flags.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (job.ExcludeFolders.Count > 0)
        {
            args.Add("/XD");
            args.AddRange(job.ExcludeFolders);
        }
        if (job.ExcludeFiles.Count > 0)
        {
            args.Add("/XF");
            args.AddRange(job.ExcludeFiles);
        }
        return args;
    }
}
