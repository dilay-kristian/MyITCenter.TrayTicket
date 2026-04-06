using System.Drawing;
using System.Windows;
using MyitCenter.TrayTicketTool.Helpers;
using MyitCenter.TrayTicketTool.Models;
using MyitCenter.TrayTicketTool.Services;

namespace MyitCenter.TrayTicketTool.Views;

public partial class TicketReplyWindow : Window
{
    private readonly TicketStatusInfo _ticket;
    private readonly TicketReplyService _replyService;
    private readonly IScreenshotService _screenshotService = new ScreenshotService();
    private Bitmap? _currentScreenshot;

    public TicketReplyWindow(TicketStatusInfo ticket, TicketReplyService replyService)
    {
        InitializeComponent();
        _ticket = ticket;
        _replyService = replyService;

        HeaderText.Text = $"Antwort zu {ticket.TicketNumber}";
        SubjectText.Text = ticket.Subject;
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        _currentScreenshot?.Dispose();
        _currentScreenshot = null;

        Hide();
        Thread.Sleep(300);

        try
        {
            if (RegionMode.IsChecked == true)
            {
                var regionWindow = new RegionSelectionWindow();
                var result = regionWindow.ShowDialog();
                if (result == true && regionWindow.SelectedRegion.HasValue)
                {
                    var r = regionWindow.SelectedRegion.Value;
                    var rect = new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                    _currentScreenshot = _screenshotService.CaptureRegion(rect);
                }
            }
            else if (FullScreenMode.IsChecked == true)
            {
                _currentScreenshot = _screenshotService.CaptureFullScreen();
            }
            else if (WindowMode.IsChecked == true)
            {
                var windows = _screenshotService.GetVisibleWindows();
                var dialog = new WindowSelectionDialog(windows);
                Show();
                if (dialog.ShowDialog() == true && dialog.SelectedWindow != null)
                {
                    Hide();
                    Thread.Sleep(200);
                    _currentScreenshot = _screenshotService.CaptureWindow(dialog.SelectedWindow.Handle);
                }
            }

            if (_currentScreenshot != null)
            {
                ScreenshotPreview.Source = BitmapToImageSourceConverter.Convert(_currentScreenshot);
                NoScreenshotText.Visibility = Visibility.Collapsed;
                StatusText.Text = "Screenshot aufgenommen.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            Show();
            Activate();
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var message = MessageBox.Text.Trim();
        byte[]? screenshotPng = _currentScreenshot != null
            ? BitmapToImageSourceConverter.ToByteArray(_currentScreenshot)
            : null;

        if (string.IsNullOrEmpty(message) && screenshotPng == null)
        {
            System.Windows.MessageBox.Show(
                "Bitte geben Sie eine Nachricht ein oder nehmen Sie einen Screenshot auf.",
                "Hinweis",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SendButton.IsEnabled = false;
        CaptureButton.IsEnabled = false;
        StatusText.Text = "Wird gesendet...";

        try
        {
            await _replyService.SendReplyAsync(
                _ticket.TicketId,
                string.IsNullOrEmpty(message) ? null : message,
                screenshotPng);

            System.Windows.MessageBox.Show(
                $"Nachricht zu {_ticket.TicketNumber} wurde erfolgreich gesendet.",
                "Gesendet",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _currentScreenshot?.Dispose();
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Fehler: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Fehler beim Senden:\n{ex.Message}",
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SendButton.IsEnabled = true;
            CaptureButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _currentScreenshot?.Dispose();
        Close();
    }
}
