using System.Drawing;
using System.Threading;
using System.Windows;
using MyitCenter.TrayTicketTool.Views;
using Forms = System.Windows.Forms;

namespace MyitCenter.TrayTicketTool;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private Forms.NotifyIcon? _notifyIcon;
    private TicketWindow? _ticketWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single Instance per User-Session (wichtig fuer Terminal Server)
        var sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        _mutex = new Mutex(true, $"MyitCenter.TrayTicketTool_Session{sessionId}", out bool isNew);

        if (!isNew)
        {
            System.Windows.MessageBox.Show(
                "Das Ticket Tool laeuft bereits.",
                "myit.center",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        CreateTrayIcon();
    }

    private void CreateTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "myit.center - Ticket Tool",
            Visible = true
        };

        var contextMenu = new Forms.ContextMenuStrip();

        var ticketItem = new Forms.ToolStripMenuItem("Ticket erstellen");
        ticketItem.Click += (_, _) => OpenTicketWindow();
        ticketItem.Font = new Font(ticketItem.Font, System.Drawing.FontStyle.Bold);

        var exitItem = new Forms.ToolStripMenuItem("Beenden");
        exitItem.Click += (_, _) => ExitApplication();

        contextMenu.Items.Add(ticketItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => OpenTicketWindow();
    }

    private Icon LoadIcon()
    {
        var resourceUri = new Uri("pack://application:,,,/Resources/tray-icon.ico");
        var streamInfo = GetResourceStream(resourceUri);
        if (streamInfo != null)
            return new Icon(streamInfo.Stream);

        return SystemIcons.Information;
    }

    private void OpenTicketWindow()
    {
        if (_ticketWindow is { IsVisible: true })
        {
            _ticketWindow.Activate();
            return;
        }

        _ticketWindow = new TicketWindow();
        _ticketWindow.Closed += (_, _) => _ticketWindow = null;
        _ticketWindow.Show();
        _ticketWindow.Activate();
    }

    private void ExitApplication()
    {
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
