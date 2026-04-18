using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MyitCenter.TrayTicketTool.Helpers;
using MyitCenter.TrayTicketTool.Models;
using MyitCenter.TrayTicketTool.Services;

namespace MyitCenter.TrayTicketTool.Views;

public partial class TicketDetailWindow : Window
{
    private readonly TicketStatusInfo _ticket;
    private readonly TicketMessagesService _messagesService;
    private readonly TicketReplyService _replyService;
    private readonly AgentConfig _agentConfig;
    private readonly IScreenshotService _screenshotService = new ScreenshotService();
    private Bitmap? _replyScreenshot;
    private readonly List<string> _attachmentPaths = new();

    public TicketDetailWindow(
        TicketStatusInfo ticket,
        TicketMessagesService messagesService,
        TicketReplyService replyService,
        AgentConfig agentConfig)
    {
        InitializeComponent();
        _ticket = ticket;
        _messagesService = messagesService;
        _replyService = replyService;
        _agentConfig = agentConfig;

        TicketNumberText.Text = ticket.TicketNumber;
        SubjectText.Text = ticket.Subject;

        Loaded += async (_, _) => await LoadMessages();
    }

    private async Task LoadMessages()
    {
        StatusText.Text = "Nachrichten werden geladen...";

        try
        {
            var (subject, messages) = await _messagesService.GetMessagesAsync(_ticket.TicketId);

            if (subject != null)
                SubjectText.Text = subject;

            MessagesPanel.Children.Clear();

            if (messages.Count == 0)
            {
                MessagesPanel.Children.Add(new TextBlock
                {
                    Text = "Keine Nachrichten vorhanden.",
                    Foreground = (System.Windows.Media.SolidColorBrush)FindResource("SubtextBrush"),
                    FontSize = 13,
                    Margin = new Thickness(12, 20, 12, 20),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                });
            }
            else
            {
                foreach (var msg in messages)
                    MessagesPanel.Children.Add(CreateMessageBubble(msg));
            }

            StatusText.Text = "";

            // Nach unten scrollen
            MessagesScroll.ScrollToEnd();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Fehler: {ex.Message}";
        }
    }

    private Border CreateMessageBubble(TicketMessage msg)
    {
        var isAgent = msg.From == "agent";

        var bubble = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(isAgent
                ? System.Windows.Media.Color.FromRgb(0xED, 0xE9, 0xFE)  // Helles Lila f\u00fcr Support
                : System.Windows.Media.Color.FromRgb(0xF0, 0xF2, 0xF5)), // Hellgrau f\u00fcr Kunde
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(
                isAgent ? 8 : 40,   // Links
                4,                   // Oben
                isAgent ? 40 : 8,   // Rechts
                4),                  // Unten
            HorizontalAlignment = isAgent ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Right
        };

        var content = new StackPanel();

        // Author + Zeit
        var header = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        header.Children.Add(new TextBlock
        {
            Text = isAgent ? $"Support ({msg.Author})" : msg.Author,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Foreground = new System.Windows.Media.SolidColorBrush(isAgent
                ? System.Windows.Media.Color.FromRgb(0x6C, 0x5C, 0xE7)
                : System.Windows.Media.Color.FromRgb(0x37, 0x41, 0x51))
        });
        header.Children.Add(new TextBlock
        {
            Text = $"  {msg.CreatedAt:dd.MM.yyyy HH:mm}",
            FontSize = 10,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xAA, 0xAA, 0xAA)),
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(header);

        // Nachricht
        if (!string.IsNullOrWhiteSpace(msg.Body))
        {
            content.Children.Add(new TextBlock
            {
                Text = msg.Body,
                FontSize = 13,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x2E)),
                TextWrapping = TextWrapping.Wrap
            });
        }

        // Anhaenge
        if (msg.Attachments.Count > 0)
        {
            var attachPanel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var att in msg.Attachments)
            {
                var link = new TextBlock
                {
                    Text = $"📎 {att.Name}",
                    FontSize = 11,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x29, 0x80, 0xB9)),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    TextDecorations = TextDecorations.Underline
                };
                var capturedAtt = att;
                link.MouseLeftButtonUp += async (_, _) => await DownloadAttachment(capturedAtt);
                attachPanel.Children.Add(link);
            }
            content.Children.Add(attachPanel);
        }

        bubble.Child = content;
        return bubble;
    }

    private async Task DownloadAttachment(TicketAttachment attachment)
    {
        try
        {
            StatusText.Text = $"Lade {attachment.Name}...";

            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _agentConfig.AgentToken);

            var url = attachment.Url;
            if (!url.StartsWith("http"))
                url = $"{_agentConfig.ApiUrl.TrimEnd('/')}{url}";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text = $"Download fehlgeschlagen: HTTP {(int)response.StatusCode}";
                return;
            }

            // In temp speichern und oeffnen
            var tempDir = Path.Combine(Path.GetTempPath(), "MyitCenter", "Attachments");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, attachment.Name);

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(tempFile, bytes);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            });

            StatusText.Text = "";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Download-Fehler: {ex.Message}";
            LogService.Error($"Attachment-Download fehlgeschlagen: {attachment.Name}", ex);
        }
    }

    private void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        _replyScreenshot?.Dispose();
        _replyScreenshot = null;

        Hide();
        Thread.Sleep(300);

        try
        {
            var regionWindow = new RegionSelectionWindow();
            var result = regionWindow.ShowDialog();
            if (result == true && regionWindow.SelectedRegion.HasValue)
            {
                var r = regionWindow.SelectedRegion.Value;
                var rect = new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                _replyScreenshot = _screenshotService.CaptureRegion(rect);
                UpdateAttachmentList();
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Screenshot-Fehler: {ex.Message}";
        }
        finally
        {
            Show();
            Activate();
        }
    }

    private void AttachFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Datei anhängen",
            Multiselect = true,
            Filter = "Alle Dateien (*.*)|*.*|Log-Dateien (*.log;*.txt)|*.log;*.txt|Bilder (*.png;*.jpg)|*.png;*.jpg"
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                if (!_attachmentPaths.Contains(file))
                    _attachmentPaths.Add(file);
            }
            UpdateAttachmentList();
        }
    }

    private void UpdateAttachmentList()
    {
        var parts = new List<string>();
        if (_replyScreenshot != null)
            parts.Add("Screenshot");
        foreach (var path in _attachmentPaths)
            parts.Add(Path.GetFileName(path));

        AttachmentList.Text = parts.Count > 0
            ? $"Anhänge: {string.Join(", ", parts)}"
            : "";
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var message = ReplyBox.Text.Trim();
        byte[]? screenshotPng = _replyScreenshot != null
            ? BitmapToImageSourceConverter.ToByteArray(_replyScreenshot)
            : null;

        if (string.IsNullOrEmpty(message) && screenshotPng == null && _attachmentPaths.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "Bitte eine Nachricht eingeben, einen Screenshot aufnehmen oder eine Datei anhängen.",
                "Hinweis",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SendButton.IsEnabled = false;
        StatusText.Text = "Wird gesendet...";

        try
        {
            await _replyService.SendReplyAsync(
                _ticket.TicketId,
                string.IsNullOrEmpty(message) ? null : message,
                screenshotPng,
                _attachmentPaths.Count > 0 ? _attachmentPaths : null);

            // Reset
            ReplyBox.Text = "";
            _replyScreenshot?.Dispose();
            _replyScreenshot = null;
            _attachmentPaths.Clear();
            UpdateAttachmentList();

            StatusText.Text = "Gesendet!";

            // Nachrichten neu laden
            await LoadMessages();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Fehler: {ex.Message}";
            LogService.Error("Reply senden fehlgeschlagen", ex);
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _replyScreenshot?.Dispose();
        base.OnClosed(e);
    }
}
