using System.Runtime.Versioning;
using SyncSentinel.Core;

namespace SyncSentinel.Tests;

/// <summary>
/// The single-instance guard: the named-mutex + named-event protocol that makes a
/// second launch surface (or quit) the running instance instead of opening a
/// second window. OS-primitive logic, so it's tested directly — the window
/// marshaling that reacts to it stays in the shell's Program.Main.
/// </summary>
[SupportedOSPlatform("windows")] // SyncSentinel is Windows-only (robocopy)
public sealed class SingleInstanceGuardTests
{
    // Unique names per test so the system-wide handles never collide across tests.
    private readonly string _id = Guid.NewGuid().ToString("N");
    private SingleInstanceGuard New() =>
        new($@"Local\sst-{_id}", $@"Local\sst-show-{_id}", $@"Local\sst-quit-{_id}");

    [Fact]
    public void Only_the_first_guard_acquires_the_single_slot()
    {
        using var first = New();
        Assert.True(first.TryAcquire());

        using var second = New();
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void The_slot_is_freed_once_the_primary_is_disposed()
    {
        var first = New();
        Assert.True(first.TryAcquire());
        first.Dispose();

        using var next = New();
        Assert.True(next.TryAcquire());
    }

    [Fact]
    public async Task Signal_Show_from_a_second_instance_invokes_the_primary_OnShow()
    {
        using var primary = New();
        Assert.True(primary.TryAcquire());
        var shown = new TaskCompletionSource();
        primary.Listen(onShow: () => shown.TrySetResult(), onQuit: () => { });

        using var second = New();
        Assert.False(second.TryAcquire()); // one is already running
        second.Signal(InstanceSignal.Show);

        await shown.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Signal_Quit_from_a_second_instance_invokes_the_primary_OnQuit()
    {
        using var primary = New();
        Assert.True(primary.TryAcquire());
        var quit = new TaskCompletionSource();
        primary.Listen(onShow: () => { }, onQuit: () => quit.TrySetResult());

        using var second = New();
        Assert.False(second.TryAcquire());
        second.Signal(InstanceSignal.Quit);

        await quit.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Signalling_with_no_primary_present_does_not_throw()
    {
        using var lonely = New(); // no instance ever acquired, so no event exists

        var ex = Record.Exception(() => lonely.Signal(InstanceSignal.Show));

        Assert.Null(ex);
    }
}
