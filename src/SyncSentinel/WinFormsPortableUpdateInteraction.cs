using System.Diagnostics;
using SyncSentinel.Core;

namespace SyncSentinel;

/// <summary>Maps the portable update decision onto native WinForms UI.</summary>
internal sealed class WinFormsPortableUpdateInteraction : IPortableUpdateInteraction
{
    private MainForm? _form;

    public void SetForm(MainForm form) => _form = form;

    public Task<PortableUpdateChoice> PromptAsync(
        UpdateRelease release,
        CancellationToken cancellationToken = default)
    {
        if (_form is null)
        {
            return Task.FromResult(PortableUpdateChoice.RemindLater);
        }

        var completion = new TaskCompletionSource<PortableUpdateChoice>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void ShowPrompt()
        {
            var choice = MessageBox.Show(
                $"SyncSentinel {release.Version} is available. This is a portable copy, so it will never overwrite itself.\n\n"
                + "Yes — view the release\nNo — skip this version\nCancel — remind me later",
                "SyncSentinel update available",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Information);
            var action = choice switch
            {
                DialogResult.Yes => PortableUpdateChoice.ViewRelease,
                DialogResult.No => PortableUpdateChoice.SkipVersion,
                _ => PortableUpdateChoice.RemindLater,
            };
            if (action == PortableUpdateChoice.ViewRelease)
            {
                Process.Start(new ProcessStartInfo(release.ReleaseUrl) { UseShellExecute = true });
            }
            completion.SetResult(action);
        }

        if (_form.InvokeRequired)
        {
            _form.BeginInvoke(ShowPrompt);
        }
        else
        {
            ShowPrompt();
        }
        return completion.Task;
    }
}
