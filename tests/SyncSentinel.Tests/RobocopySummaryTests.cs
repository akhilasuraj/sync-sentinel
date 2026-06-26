using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public class RobocopySummaryTests
{
    // The "Files :" row columns are: Total Copied Skipped Mismatch FAILED Extras
    private static readonly string[] FullSummary =
    [
        "------------------------------------------------------------------------------",
        "",
        "               Total    Copied   Skipped  Mismatch    FAILED    Extras",
        "    Dirs :         6         3         3         0         0         1",
        "   Files :         4         4         0         0         0         1",
        "   Bytes :        12        12         0         0         0         6",
        "   Times :   0:00:00   0:00:00                       0:00:00   0:00:00",
    ];

    [Fact]
    public void Parses_copied_and_extra_from_the_files_row()
    {
        var s = RobocopySummary.Parse(FullSummary);

        Assert.Equal(4, s.FilesCopied);
        Assert.Equal(0, s.FilesSkipped);
        Assert.Equal(0, s.FilesFailed);
        Assert.Equal(1, s.FilesExtra);
    }

    [Fact]
    public void Parses_skips_and_failures()
    {
        var lines = new[] { "   Files :       100         5        95         0         3         0" };

        var s = RobocopySummary.Parse(lines);

        Assert.Equal(5, s.FilesCopied);
        Assert.Equal(95, s.FilesSkipped);
        Assert.Equal(3, s.FilesFailed);
        Assert.Equal(0, s.FilesExtra);
    }

    [Fact]
    public void Returns_empty_when_there_is_no_summary()
    {
        var lines = new[] { "some directory scroll", "no summary here" };

        Assert.Equal(RobocopySummary.Empty, RobocopySummary.Parse(lines));
    }
}
