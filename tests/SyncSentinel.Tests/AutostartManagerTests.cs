using System.Runtime.Versioning;
using Microsoft.Win32;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

[SupportedOSPlatform("windows")] // SyncSentinel is Windows-only (robocopy)
public sealed class AutostartManagerTests : IDisposable
{
    private readonly string _keyPath = $@"Software\SyncSentinelTest\{Guid.NewGuid():N}";
    private const string ValueName = "SyncSentinel";
    private const string Command = "\"C:\\app\\SyncSentinel.exe\" --tray";

    private AutostartManager New() => new(_keyPath, ValueName, Command);

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

    [Fact]
    public void Enable_makes_it_enabled_and_writes_the_command()
    {
        New().Enable();

        Assert.True(New().IsEnabled());
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        Assert.Equal(Command, key!.GetValue(ValueName));
    }

    [Fact]
    public void Disable_removes_it()
    {
        var mgr = New();
        mgr.Enable();

        mgr.Disable();

        Assert.False(New().IsEnabled());
    }

    [Fact]
    public void Disable_when_absent_does_not_throw()
    {
        var ex = Record.Exception(() => New().Disable());
        Assert.Null(ex);
    }

    [Fact]
    public void Apply_toggles_based_on_the_flag()
    {
        var mgr = New();

        mgr.Apply(true);
        Assert.True(mgr.IsEnabled());

        mgr.Apply(false);
        Assert.False(mgr.IsEnabled());
    }
}
