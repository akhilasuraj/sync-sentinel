using System.Runtime.Versioning;
using System.Threading;

namespace SyncSentinel.Core;

/// <summary>Which thing a second launch asks the running instance to do.</summary>
public enum InstanceSignal { Show, Quit }

/// <summary>
/// Enforces a single running instance and carries the second-launch protocol: the
/// first process <see cref="TryAcquire"/>s the named mutex and <see cref="Listen"/>s
/// for signals; a later launch fails to acquire and <see cref="Signal"/>s the
/// primary to surface its window (or quit). The mutex + named events live here so
/// the protocol is testable; the shell's Program.Main supplies the window actions.
/// Names are injectable so tests use throwaway handles.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SingleInstanceGuard : IDisposable
{
    public const string DefaultMutexName = @"Local\SyncSentinel.SingleInstance";
    public const string DefaultShowEventName = @"Local\SyncSentinel.Show";
    public const string DefaultQuitEventName = @"Local\SyncSentinel.Quit";

    private readonly string _mutexName;
    private readonly string _showEventName;
    private readonly string _quitEventName;
    private readonly ManualResetEvent _stop = new(false);
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private EventWaitHandle? _quitEvent;
    private Thread? _listener;

    public SingleInstanceGuard()
        : this(DefaultMutexName, DefaultShowEventName, DefaultQuitEventName)
    {
    }

    public SingleInstanceGuard(string mutexName, string showEventName, string quitEventName)
    {
        _mutexName = mutexName;
        _showEventName = showEventName;
        _quitEventName = quitEventName;
    }

    /// <summary>True if we are the single instance; false if one is already running.</summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (createdNew)
        {
            // Own the named events so a later launch can OpenExisting + signal us.
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _showEventName);
            _quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _quitEventName);
        }
        return createdNew;
    }

    /// <summary>
    /// From a non-primary instance: ask the running one to surface its window
    /// (<see cref="InstanceSignal.Show"/>) or quit. Best-effort — a no-op if no
    /// primary is listening yet.
    /// </summary>
    public void Signal(InstanceSignal signal)
    {
        var name = signal == InstanceSignal.Quit ? _quitEventName : _showEventName;
        try
        {
            using var handle = EventWaitHandle.OpenExisting(name);
            handle.Set();
        }
        catch
        {
            // No primary has created the event yet — nothing to signal.
        }
    }

    /// <summary>
    /// On the primary: react to second-launch signals until quit or disposed.
    /// <paramref name="onShow"/> may fire repeatedly; <paramref name="onQuit"/>
    /// fires once and ends the loop.
    /// </summary>
    public void Listen(Action onShow, Action onQuit)
    {
        var handles = new WaitHandle[] { _showEvent!, _quitEvent!, _stop };
        _listener = new Thread(() =>
        {
            while (true)
            {
                var signaled = WaitHandle.WaitAny(handles);
                try
                {
                    if (signaled == 0) { onShow(); }
                    else if (signaled == 1) { onQuit(); break; }
                    else { break; } // _stop — disposing
                }
                catch
                {
                    break;
                }
            }
        })
        { IsBackground = true };
        _listener.Start();
    }

    public void Dispose()
    {
        _stop.Set();
        _listener?.Join(TimeSpan.FromSeconds(1));
        _showEvent?.Dispose();
        _quitEvent?.Dispose();
        _stop.Dispose();
        _mutex?.Dispose();
    }
}
