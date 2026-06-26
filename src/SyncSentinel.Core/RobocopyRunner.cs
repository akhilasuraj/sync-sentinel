using System.Diagnostics;

namespace SyncSentinel.Core;

/// <summary>
/// Runs a <see cref="BackupJob"/> through robocopy: composes the command, spawns
/// the process, streams each stdout/stderr line to <paramref name="onLine"/> as
/// it arrives (the seam the SignalR layer forwards to the UI), and returns the
/// parsed <see cref="RobocopyResult"/>.
/// </summary>
public sealed class RobocopyRunner
{
    public async Task<RobocopyResult> RunAsync(BackupJob job, Action<string> onLine, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("robocopy.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in RobocopyCommand.Build(job))
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);
        process.WaitForExit(); // drain any pending async output

        return RobocopyResult.FromExitCode(process.ExitCode);
    }
}
