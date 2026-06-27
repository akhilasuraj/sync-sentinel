using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace SyncSentinel;

/// <summary>
/// The app window: a full-bleed WebView2 pointed at the loopback UI, plus a
/// system-tray icon. Starts hidden under --tray (login autostart). Closing the
/// window hides to tray (scheduling keeps running); real exit is via the tray
/// menu. <see cref="ShowExternally"/> surfaces the window when a second instance
/// is launched.
/// </summary>
internal sealed class MainForm : Form
{
    private const string WebView2DownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    private readonly NotifyIcon _tray;
    private bool _exitRequested;
    private bool _allowVisible;
    private bool _webViewFailed;
    private bool _webViewWarned;

    public MainForm(string url, bool startHidden)
    {
        _allowVisible = !startHidden;

        Text = "SyncSentinel";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;

        var web = new WebView2 { Dock = DockStyle.Fill };
        web.CoreWebView2InitializationCompleted += OnWebViewInitialized;
        Controls.Add(web);
        web.Source = new Uri(url);

        _tray = new NotifyIcon
        {
            Text = "SyncSentinel",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        _tray.DoubleClick += (_, _) => ShowWindow();
    }

    // Keeps the window hidden on startup under --tray until something shows it.
    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(value && _allowVisible);

    /// <summary>Surface the window (called when a second instance is launched).</summary>
    public void ShowExternally() => ShowWindow();

    /// <summary>
    /// Request a real exit — bypasses close-to-tray. Called when a second launch
    /// signals <c>--quit</c> (the installer/uninstaller stopping us before it
    /// touches files).
    /// </summary>
    public void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _exitRequested = true;
            Close();
        });
        return menu;
    }

    private void ShowWindow()
    {
        _allowVisible = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        WarnIfWebViewFailed();
    }

    // If the WebView2 runtime can't start, the page never renders. Rather than
    // leave a blank window, explain it and offer the download — but only once the
    // window is actually shown, so a silent --tray login start never nags. Backups
    // keep running from the tray regardless.
    private void OnWebViewInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            return;
        }
        _webViewFailed = true;
        if (Visible)
        {
            WarnIfWebViewFailed();
        }
    }

    private void WarnIfWebViewFailed()
    {
        if (!_webViewFailed || _webViewWarned)
        {
            return;
        }
        _webViewWarned = true;

        var open = MessageBox.Show(
            "SyncSentinel needs the Microsoft Edge WebView2 Runtime to show its window, "
            + "and it couldn't be started on this PC.\n\n"
            + "Your backups still run in the background from the tray icon. To restore the "
            + "window, install the Evergreen WebView2 Runtime and relaunch SyncSentinel.\n\n"
            + "Open the download page now?",
            "WebView2 Runtime required",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (open == DialogResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo(WebView2DownloadUrl) { UseShellExecute = true });
            }
            catch
            {
                // Best-effort; the URL is in the message above.
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Close button hides to tray; only the tray "Exit" truly closes.
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosing(e);
    }
}
