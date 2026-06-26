using Microsoft.Web.WebView2.WinForms;

namespace SyncSentinel;

/// <summary>
/// The app window: a full-bleed WebView2 pointed at the loopback UI, plus a
/// system-tray icon. Closing the window hides to tray (scheduling keeps
/// running); real exit is via the tray menu. (Single-instance, autostart and
/// richer tray come in Phase 5.)
/// </summary>
internal sealed class MainForm : Form
{
    private readonly NotifyIcon _tray;
    private bool _exitRequested;

    public MainForm(string url)
    {
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
