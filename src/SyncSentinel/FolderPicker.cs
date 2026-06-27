using System.Runtime.Versioning;
using SyncSentinel.Core;

namespace SyncSentinel;

/// <summary>
/// The real <see cref="IFolderPicker"/>: shows the native folder dialog
/// (<see cref="FolderBrowserDialog"/>, which uses the modern Vista picker on
/// .NET Core 3.0+) on the WinForms UI thread, while the loopback HTTP request
/// thread awaits the user's choice through a
/// <see cref="TaskCompletionSource{TResult}"/>. The owning control is supplied
/// after the window exists (the DI container is built before the MainForm).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FolderPicker : IFolderPicker
{
    private Control? _ui;

    public bool Available => true;

    /// <summary>Wire up the UI thread/owner once the window exists (from Program).</summary>
    public void SetUiContext(Control ui) => _ui = ui;

    public Task<string?> PickFolderAsync(string? initialPath, string? title)
    {
        var ui = _ui;
        if (ui is null)
        {
            return Task.FromResult<string?>(null); // window not ready yet
        }

        var tcs = new TaskCompletionSource<string?>();
        ui.BeginInvoke(() =>
        {
            try
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = string.IsNullOrWhiteSpace(title) ? "Select folder" : title,
                    UseDescriptionForTitle = true,   // show the title in the title bar
                    ShowNewFolderButton = true,      // lets a destination be created
                };
                if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                {
                    dialog.InitialDirectory = initialPath;
                }
                var ok = dialog.ShowDialog(ui) == DialogResult.OK;
                tcs.SetResult(ok ? dialog.SelectedPath : null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
