namespace SyncSentinel.Core;

/// <summary>
/// A single backup unit: mirror <see cref="Source"/> to <see cref="Destination"/>
/// with robocopy. In Phase 1 the excludes are carried directly; Phase 2 resolves
/// them from named FolderExclusionSet / FileExclusionSet (see CONTEXT.md).
/// </summary>
public sealed record BackupJob
{
    public string Name { get; init; } = "";
    public required string Source { get; init; }
    public required string Destination { get; init; }

    /// <summary>The editable robocopy behavior flags (see CONTEXT.md).</summary>
    public string Flags { get; init; } = "/MIR /XJ /R:3 /W:5 /FFT /NP /NFL";

    /// <summary>Directory names to exclude (robocopy /XD).</summary>
    public IReadOnlyList<string> ExcludeFolders { get; init; } = [];

    /// <summary>File patterns to exclude (robocopy /XF).</summary>
    public IReadOnlyList<string> ExcludeFiles { get; init; } = [];
}
