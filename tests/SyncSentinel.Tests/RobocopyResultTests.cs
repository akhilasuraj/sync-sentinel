using SyncSentinel.Core;

namespace SyncSentinel.Tests;

public class RobocopyResultTests
{
    [Theory]
    [InlineData(0, RobocopyStatus.Success)]
    [InlineData(1, RobocopyStatus.Success)]
    [InlineData(3, RobocopyStatus.Success)]
    [InlineData(8, RobocopyStatus.Warning)]
    [InlineData(16, RobocopyStatus.Error)]
    [InlineData(24, RobocopyStatus.Error)]
    public void Maps_exit_code_to_status(int exitCode, RobocopyStatus expected)
    {
        var result = RobocopyResult.FromExitCode(exitCode);

        Assert.Equal(expected, result.Status);
    }
}
