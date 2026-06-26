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
    private readonly NotifyIcon _tray;
    private bool _exitRequested;
    private bool _allowVisible;

    public MainForm(string url, bool startHidden)
    {
        _allowVisible = !startHidden;

        Text = "SyncSentinel";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;

        var web = new WebView2 { Dock = DockStyle.Fill };
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
