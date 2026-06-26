using System.Text.RegularExpressions;

namespace SyncSentinel.Core;

/// <summary>
/// The file-level counts robocopy prints in its per-job summary table. Parsed
/// from the captured output so a run record can show what actually happened
/// (copied / skipped / failed / extra-deleted). Falls back to zeros when no
/// summary is present (e.g. a run that errored before printing one).
/// </summary>
public sealed record RobocopySummary(int FilesCopied, int FilesSkipped, int FilesFailed, int FilesExtra)
{
    public static readonly RobocopySummary Empty = new(0, 0, 0, 0);

    // "   Files :   <Total> <Copied> <Skipped> <Mismatch> <FAILED> <Extras>"
    private static readonly Regex FilesRow = new(
        @"^\s*Files :\s+\d+\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*$",
        RegexOptions.Compiled);

    public static RobocopySummary Parse(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var m = FilesRow.Match(line);
            if (m.Success)
            {
                return new RobocopySummary(
                    FilesCopied: int.Parse(m.Groups[1].Value),
                    FilesSkipped: int.Parse(m.Groups[2].Value),
                    // group 3 = Mismatch (not tracked), group 4 = FAILED, group 5 = Extras
                    FilesFailed: int.Parse(m.Groups[4].Value),
                    FilesExtra: int.Parse(m.Groups[5].Value));
            }
        }
        return Empty;
    }
}
