using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SyncSentinel.Core;

/// <summary>
/// Neutral seam for toggling login autostart. The shell provides the Windows
/// registry implementation (<see cref="AutostartManager"/>); the shared settings
/// endpoint depends only on this interface so Core stays free of Windows-only
/// types and tests can inject a fake.
/// </summary>
public interface IAutostart
{
    /// <summary>Set login autostart to match <paramref name="enabled"/>.</summary>
    void Apply(bool enabled);
}

/// <summary>
/// Default <see cref="IAutostart"/> that does nothing, registered in the shared
/// wiring so the settings endpoint resolves under the in-memory TestServer and on
/// non-Windows. The shell overrides it with <see cref="AutostartManager"/>.
/// </summary>
public sealed class NoOpAutostart : IAutostart
{
    public void Apply(bool enabled) { }
}

/// <summary>
/// Toggles login autostart via the per-user registry Run key (no admin needed).
/// The key path, value name and command are injectable so tests run against a
/// throwaway key instead of the real Run key.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AutostartManager : IAutostart
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "SyncSentinel";

    private readonly string _keyPath;
    private readonly string _valueName;
    private readonly string _command;

    // DI: real Run key; autostart launches the exe minimized to the tray.
    public AutostartManager(string exePath)
        : this(RunKeyPath, DefaultValueName, $"\"{exePath}\" --tray")
    {
    }

    public AutostartManager(string keyPath, string valueName, string command)
    {
        _keyPath = keyPath;
        _valueName = valueName;
        _command = command;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(_valueName) is not null;
    }

    public void Apply(bool enabled)
    {
        if (enabled)
        {
            Enable();
        }
        else
        {
            Disable();
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
        key.SetValue(_valueName, _command);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}
