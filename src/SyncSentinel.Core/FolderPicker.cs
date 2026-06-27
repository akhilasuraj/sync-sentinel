namespace SyncSentinel.Core;

/// <summary>
/// Neutral seam for choosing a folder with a native dialog. The tray shell
/// provides the Windows implementation; Core stays free of GUI types and the
/// endpoints depend only on this interface so tests can inject a fake. Same shape
/// as <see cref="IAutostart"/>.
/// </summary>
public interface IFolderPicker
{
    /// <summary>True when a real picker is wired up (the shell); false headless.</summary>
    bool Available { get; }

    /// <summary>
    /// Show the folder dialog and return the chosen absolute path, or <c>null</c> if
    /// the user cancels. <paramref name="initialPath"/> seeds the starting folder.
    /// </summary>
    Task<string?> PickFolderAsync(string? initialPath, string? title);
}

/// <summary>
/// Default <see cref="IFolderPicker"/> registered in the shared wiring: reports
/// unavailable and never picks. Used under the TestServer and in a plain dev
/// browser (no shell); the shell overrides it with the real native-dialog impl.
/// </summary>
public sealed class NoOpFolderPicker : IFolderPicker
{
    public bool Available => false;
    public Task<string?> PickFolderAsync(string? initialPath, string? title) => Task.FromResult<string?>(null);
}

/// <summary>Body of <c>POST /api/pick-folder</c>: where to start, and the dialog title.</summary>
public sealed record PickFolderRequest(string? InitialPath, string? Title);
