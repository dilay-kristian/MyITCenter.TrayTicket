using System.Drawing;
using System.IO;
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
    private TicketMessagesService? _messagesService;
    private ScreenRecordingService? _screenRecordingService;
    private ScreenRecordingOverlay? _recordingOverlay;
    private Forms.ToolStripMenuItem? _screenRecordItem;
    private Forms.ToolStripMenuItem? _myTicketsItem;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Crash-Log neben die EXE schreiben — funktioniert auch wenn alles andere fehlschlaegt
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            WriteCrashLog("AppDomain.UnhandledException", args.ExceptionObject as Exception);
        };

        try
        {
            Initialize();
        }
        catch (Exception ex)
        {
            WriteCrashLog("Startup-Exception", ex);
            System.Windows.MessageBox.Show(
                $"Fehler beim Starten:\n\n{ex.Message}\n\nDetails in crash.log neben der EXE.",
                "myit.center - Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void Initialize()
    {
        // Single Instance per User-Session (wichtig fuer Terminal Server)
        var sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        _mutex = new Mutex(true, $"MyitCenter.TrayTicketTool_Session{sessionId}", out bool isNew);

        if (!isNew)
        {
            System.Windows.MessageBox.Show(
                "Das Ticket Tool läuft bereits.",
                "myit.center",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Globaler WPF Exception-Handler
        DispatcherUnhandledException += (_, args) =>
        {
            LogService.Error("Unbehandelte Exception", args.Exception);
            WriteCrashLog("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };

        LogService.Info("=== TrayTicketTool gestartet ===");
        LogService.Info($"Version: {typeof(App).Assembly.GetName().Version}");
        LogService.Info($"User: {Environment.UserName}, Machine: {Environment.MachineName}");
        LogService.Info($"OS: {Environment.OSVersion}");
        LogService.Info($"EXE-Pfad: {Environment.ProcessPath}");

        _agentConfig = new AgentConfigService().Load();
        if (_agentConfig?.IsValid == true)
        {
            LogService.Info($"Agent-Config geladen: api_url={_agentConfig.ApiUrl}, device_id={_agentConfig.DeviceId}");

            var http = ApiHttpClient.GetInstance(_agentConfig);
            http.ConnectionStatusChanged += online =>
                Dispatcher.Invoke(() => UpdateConnectionStatus(online));

            _replyService = new TicketReplyService(_agentConfig);
            _messagesService = new TicketMessagesService(_agentConfig);
        }
        else
        {
            LogService.Warn("Keine gueltige Agent-Config gefunden — Offline-Modus");
        }

        CreateTrayIcon();
        LogService.Info("Tray-Icon erstellt");

        StartStatusPolling();
        LogService.Info("Startup abgeschlossen");
    }

    private static void WriteCrashLog(string context, Exception? ex)
    {
        try
        {
            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
            var crashLogPath = Path.Combine(
                Path.GetDirectoryName(exePath) ?? ".",
                "crash.log");

            var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}\n" +
                       $"Message: {ex?.Message}\n" +
                       $"Type: {ex?.GetType().FullName}\n" +
                       $"StackTrace:\n{ex?.StackTrace}\n" +
                       $"InnerException: {ex?.InnerException?.Message}\n" +
                       $"---\n";

            File.AppendAllText(crashLogPath, text);
        }
        catch
        {
            // Letzter Ausweg — kann nichts mehr tun
        }
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

        _screenRecordItem = new Forms.ToolStripMenuItem("Bildschirmaufnahme starten");
        _screenRecordItem.Click += (_, _) => ToggleScreenRecording();

        var logItem = new Forms.ToolStripMenuItem("Log-Datei öffnen");
        logItem.Click += (_, _) => OpenLogFile();

        var exitItem = new Forms.ToolStripMenuItem("Beenden");
        exitItem.Click += (_, _) => ExitApplication();

        contextMenu.Items.Add(ticketItem);
        contextMenu.Items.Add(_myTicketsItem);
        contextMenu.Items.Add(_screenRecordItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(logItem);
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

        if (text.Length > 127)
            text = text[..127];

        _notifyIcon.Text = text;

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
            title = "Ticket-Status geändert";
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

        _ticketListWindow = new TicketListWindow(tickets, OpenTicketDetail);
        _ticketListWindow.Closed += (_, _) => _ticketListWindow = null;
        _ticketListWindow.Show();
        _ticketListWindow.Activate();
    }

    private void OpenTicketDetail(TicketStatusInfo ticket)
    {
        if (_replyService == null || _messagesService == null) return;

        var detailWindow = new TicketDetailWindow(ticket, _messagesService, _replyService, _agentConfig!);
        detailWindow.Closed += (_, _) =>
        {
            _ = _statusService?.PollAsync();
        };
        detailWindow.Show();
        detailWindow.Activate();
    }

    private void ToggleScreenRecording()
    {
        if (_screenRecordingService?.IsRecording == true)
            StopScreenRecording();
        else
            StartScreenRecording();
    }

    private void StartScreenRecording()
    {
        try
        {
            _screenRecordingService ??= new ScreenRecordingService();

            _screenRecordingService.RecordingCompleted += filePath =>
            {
                Dispatcher.Invoke(() =>
                {
                    _recordingOverlay?.Close();
                    _recordingOverlay = null;
                    _screenRecordItem!.Text = "Bildschirmaufnahme starten";

                    OfferVideoToTicket(filePath);
                });
            };

            _screenRecordingService.RecordingFailed += error =>
            {
                Dispatcher.Invoke(() =>
                {
                    _recordingOverlay?.Close();
                    _recordingOverlay = null;
                    _screenRecordItem!.Text = "Bildschirmaufnahme starten";

                    System.Windows.MessageBox.Show(
                        $"Bildschirmaufnahme fehlgeschlagen:\n{error}",
                        "Fehler",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            };

            _screenRecordingService.StartRecording();

            _screenRecordItem!.Text = "Bildschirmaufnahme stoppen";

            _recordingOverlay = new ScreenRecordingOverlay();
            _recordingOverlay.StopRequested += () => Dispatcher.Invoke(StopScreenRecording);
            _recordingOverlay.Show();

            _notifyIcon?.ShowBalloonTip(2000, "Bildschirmaufnahme gestartet",
                "Klicken Sie auf Stop im Overlay oder im Tray-Menü.",
                Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            LogService.Error("Bildschirmaufnahme Start fehlgeschlagen", ex);
            System.Windows.MessageBox.Show(
                $"Fehler beim Starten der Aufnahme:\n{ex.Message}",
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void OfferVideoToTicket(string filePath)
    {
        // Fragen ob an Ticket anhaengen
        var result = System.Windows.MessageBox.Show(
            $"Bildschirmaufnahme gespeichert:\n{filePath}\n\nSoll der Dateipfad an ein Ticket angehängt werden?",
            "Aufnahme gespeichert",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes || _replyService == null)
        {
            // Nur im Explorer anzeigen
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            return;
        }

        try
        {
            var tickets = _statusService?.CurrentTickets ?? new List<TicketStatusInfo>();

            // Falls keine gecachten Tickets, nochmal pollen
            if (tickets.Count == 0 && _statusService != null)
            {
                await _statusService.PollAsync();
                tickets = _statusService.CurrentTickets;
            }

            if (tickets.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "Keine offenen Tickets vorhanden.",
                    "Hinweis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var listWindow = new TicketListWindow(tickets, ticket =>
            {
                _ = SendVideoPathToTicket(ticket, filePath);
            });
            listWindow.Title = "Video an Ticket anhängen";
            listWindow.Show();
        }
        catch (Exception ex)
        {
            LogService.Error("Video-Ticket Zuordnung fehlgeschlagen", ex);
            System.Windows.MessageBox.Show($"Fehler: {ex.Message}", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SendVideoPathToTicket(TicketStatusInfo ticket, string filePath)
    {
        if (_replyService == null) return;

        try
        {
            var hostname = Environment.MachineName;
            var message = $"Bildschirmaufnahme erstellt.\n\nDateipfad auf {hostname}:\n{filePath}";

            await _replyService.SendReplyAsync(ticket.TicketId, message, null);

            System.Windows.MessageBox.Show(
                $"Video-Pfad wurde an {ticket.TicketNumber} angehängt.\n\n" +
                $"Ein Support-Mitarbeiter wird sich das Video anschauen.",
                "Erfolgreich",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            LogService.Info($"Video-Pfad an Ticket {ticket.TicketNumber} gesendet: {filePath}");
        }
        catch (Exception ex)
        {
            LogService.Error($"Video-Pfad senden fehlgeschlagen: {ticket.TicketNumber}", ex);
            System.Windows.MessageBox.Show($"Fehler beim Senden: {ex.Message}", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopScreenRecording()
    {
        _screenRecordingService?.StopRecording();
        // RecordingCompleted-Event raeumt auf
    }

    private void UpdateConnectionStatus(bool online)
    {
        if (_notifyIcon == null) return;

        // Tray-Tooltip aktualisieren
        var status = online ? "Verbunden" : "Offline";
        var currentText = _notifyIcon.Text ?? "";
        if (currentText.Contains('\n'))
            currentText = currentText[..currentText.IndexOf('\n')];
        var tooltip = $"{currentText}\nStatus: {status}";
        if (tooltip.Length > 127) tooltip = tooltip[..127];
        _notifyIcon.Text = tooltip;

        if (online)
        {
            _notifyIcon.ShowBalloonTip(2000, "Verbindung hergestellt",
                "Die Verbindung zum Server wurde wiederhergestellt.",
                Forms.ToolTipIcon.Info);
        }
        else
        {
            _notifyIcon.ShowBalloonTip(3000, "Verbindung unterbrochen",
                "Der Server ist nicht erreichbar. Tickets werden lokal gespeichert.",
                Forms.ToolTipIcon.Warning);
        }
    }

    private void OpenLogFile()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyitCenter", "TrayTicketTool", "app.log");

        if (File.Exists(logPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true
            });
        }
        else
        {
            System.Windows.MessageBox.Show(
                $"Log-Datei nicht gefunden:\n{logPath}",
                "Log",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void ExitApplication()
    {
        LogService.Info("=== TrayTicketTool beendet ===");
        _screenRecordingService?.Dispose();
        _recordingOverlay?.Close();
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
