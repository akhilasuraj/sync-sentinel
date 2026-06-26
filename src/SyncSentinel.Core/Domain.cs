namespace SyncSentinel.Core;

// The persisted domain model (see CONTEXT.md). A Job references exclusion sets by
// id and may override the global flags; JobResolver flattens it into the
// BackupJob the robocopy runner consumes ("effective command").

/// <summary>A named, reusable list of directory names to exclude (robocopy /XD).</summary>
public sealed record FolderExclusionSet
{
    public string Id { get; init; } = ""; // server-assigned on create
    public required string Name { get; init; }
    public IReadOnlyList<string> Folders { get; init; } = [];
}

/// <summary>A named, reusable list of file patterns to exclude (robocopy /XF).</summary>
public sealed record FileExclusionSet
{
    public string Id { get; init; } = ""; // server-assigned on create
    public required string Name { get; init; }
    public IReadOnlyList<string> Patterns { get; init; } = [];
}

/// <summary>
/// One backup unit: a source mirrored to a destination, with attached exclusion
/// sets (by id), an interval, an optional flags override, and an enabled state.
/// </summary>
public sealed record Job
{
    public string Id { get; init; } = ""; // server-assigned on create
    public required string Name { get; init; }
    public required string Source { get; init; }
    public required string Destination { get; init; }
    public IReadOnlyList<string> FolderSetIds { get; init; } = [];
    public IReadOnlyList<string> FileSetIds { get; init; } = [];

    /// <summary>When null, the run uses <see cref="GlobalSettings.DefaultFlags"/>.</summary>
    public string? FlagsOverride { get; init; }

    public int IntervalMinutes { get; init; } = 15;
    public bool Enabled { get; init; } = true;
}

/// <summary>Run-history retention bounds (see CONTEXT.md / DESIGN.md §6).</summary>
public sealed record RetentionSettings
{
    public int RunsPerJob { get; init; } = 100;
    public int Days { get; init; } = 30;
}

/// <summary>App-wide settings and the defaults a job inherits.</summary>
public sealed record GlobalSettings
{
    public string DefaultFlags { get; init; } = "/MIR /XJ /R:3 /W:5 /FFT /NP /NFL";
    public int DefaultIntervalMinutes { get; init; } = 15;
    public int MaxConcurrent { get; init; } = 1;
    public RetentionSettings Retention { get; init; } = new();
    public bool Autostart { get; init; } = true;
}

/// <summary>The full persisted configuration (serialized to config.json).</summary>
public sealed record SyncSentinelConfig
{
    public IReadOnlyList<Job> Jobs { get; init; } = [];
    public IReadOnlyList<FolderExclusionSet> FolderSets { get; init; } = [];
    public IReadOnlyList<FileExclusionSet> FileSets { get; init; } = [];
    public GlobalSettings Settings { get; init; } = new();
}
