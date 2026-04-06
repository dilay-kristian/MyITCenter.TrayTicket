using System.Drawing;
using System.Threading;
using System.Windows;
using MyitCenter.TrayTicketTool.Models;
using MyitCenter.TrayTicketTool.Services;
using MyitCenter.TrayTicketTool.Views;
using Forms = System.Windows.Forms;

namespace MyitCenter.TrayTicketTool;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private Forms.NotifyIcon? _notifyIcon;
    private TicketWindow? _ticketWindow;
    private TicketListWindow? _ticketListWindow;

    private AgentConfig? _agentConfig;
    private TicketStatusService? _statusService;
    private TicketReplyService? _replyService;
    private Forms.ToolStripMenuItem? _myTicketsItem;

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

        _agentConfig = new AgentConfigService().Load();
        if (_agentConfig?.IsValid == true)
        {
            _replyService = new TicketReplyService(_agentConfig);
        }

        CreateTrayIcon();
        StartStatusPolling();
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

        _myTicketsItem = new Forms.ToolStripMenuItem("Meine Tickets");
        _myTicketsItem.Click += (_, _) => OpenTicketListWindow();
        _myTicketsItem.Visible = _agentConfig?.EnableTicketStatus == true;

        var exitItem = new Forms.ToolStripMenuItem("Beenden");
        exitItem.Click += (_, _) => ExitApplication();

        contextMenu.Items.Add(ticketItem);
        contextMenu.Items.Add(_myTicketsItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => OpenTicketWindow();
    }

    private void StartStatusPolling()
    {
        if (_agentConfig?.IsValid != true || !_agentConfig.EnableTicketStatus)
            return;

        _statusService = new TicketStatusService(_agentConfig);

        _statusService.TicketsUpdated += tickets =>
        {
            Dispatcher.Invoke(() => UpdateTrayText(tickets));
        };

        _statusService.TicketStatusChanged += ticket =>
        {
            Dispatcher.Invoke(() => ShowBalloon(ticket));
        };

        _ = _statusService.StartAsync();
    }

    private void UpdateTrayText(List<TicketStatusInfo> tickets)
    {
        if (_notifyIcon == null) return;

        string text;
        if (tickets.Count == 0)
        {
            text = "myit.center - Keine offenen Tickets";
        }
        else if (tickets.Count == 1)
        {
            var t = tickets[0];
            text = $"myit.center - 1 Ticket\n{t.TicketNumber}: {t.StatusLabel}";
        }
        else
        {
            var withReply = tickets.Count(t => t.HasAgentReply);
            text = $"myit.center - {tickets.Count} offene Tickets";
            if (withReply > 0)
                text += $"\n{withReply} mit neuer Antwort";
        }

        // NotifyIcon.Text max 128 Zeichen
        if (text.Length > 127)
            text = text[..127];

        _notifyIcon.Text = text;

        // "Meine Tickets" Menuepunkt aktualisieren
        if (_myTicketsItem != null)
        {
            var count = tickets.Count;
            _myTicketsItem.Text = count > 0
                ? $"Meine Tickets ({count})"
                : "Meine Tickets";
        }
    }

    private void ShowBalloon(TicketStatusInfo ticket)
    {
        if (_notifyIcon == null) return;

        string title;
        string message;

        if (ticket.HasAgentReply)
        {
            title = "Neue Antwort";
            message = $"{ticket.TicketNumber}: {ticket.Subject}";
        }
        else
        {
            title = "Ticket-Status geaendert";
            message = $"{ticket.TicketNumber}: {ticket.StatusLabel}";
        }

        _notifyIcon.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.Info);
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
        _ticketWindow.Closed += (_, _) =>
        {
            _ticketWindow = null;
            // Nach Ticket-Erstellung sofort Status aktualisieren
            _ = _statusService?.PollAsync();
        };
        _ticketWindow.Show();
        _ticketWindow.Activate();
    }

    private void OpenTicketListWindow()
    {
        if (_ticketListWindow is { IsVisible: true })
        {
            _ticketListWindow.Activate();
            return;
        }

        var tickets = _statusService?.CurrentTickets ?? new List<TicketStatusInfo>();

        _ticketListWindow = new TicketListWindow(tickets, OpenReplyWindow);
        _ticketListWindow.Closed += (_, _) => _ticketListWindow = null;
        _ticketListWindow.Show();
        _ticketListWindow.Activate();
    }

    private void OpenReplyWindow(TicketStatusInfo ticket)
    {
        if (_replyService == null) return;

        var replyWindow = new TicketReplyWindow(ticket, _replyService);
        replyWindow.Closed += (_, _) =>
        {
            // Nach Reply sofort aktualisieren
            _ = _statusService?.PollAsync();
        };
        replyWindow.Show();
        replyWindow.Activate();
    }

    private void ExitApplication()
    {
        _statusService?.Dispose();
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _statusService?.Dispose();
        _notifyIcon?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
