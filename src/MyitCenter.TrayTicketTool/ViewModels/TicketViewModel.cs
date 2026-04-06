using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MyitCenter.TrayTicketTool.Helpers;
using MyitCenter.TrayTicketTool.Models;
using MyitCenter.TrayTicketTool.Services;

namespace MyitCenter.TrayTicketTool.ViewModels;

public class TicketViewModel : INotifyPropertyChanged
{
    private readonly IScreenshotService _screenshotService = new ScreenshotService();
    private readonly ITicketService _ticketService;
    private readonly SystemInfoService _systemInfoService = new();
    private readonly AgentConfig? _agentConfig;

    private BitmapImage? _screenshotPreview;
    private Bitmap? _currentScreenshot;
    private string _description = string.Empty;
    private ScreenshotMode _selectedMode = ScreenshotMode.Region;
    private SystemInfo _systemInfo;
    private bool _isSubmitting;
    private string _statusMessage = string.Empty;

    public TicketViewModel()
    {
        _agentConfig = new AgentConfigService().Load();

        if (_agentConfig?.IsValid == true)
            _ticketService = new ApiTicketService(_agentConfig);
        else
            _ticketService = new LocalTicketService();

        _systemInfo = _systemInfoService.Collect();

        CaptureScreenshotCommand = new RelayCommand(
            _ => CaptureScreenshot(),
            _ => !IsSubmitting);

        SubmitTicketCommand = new RelayCommand(
            async _ => await SubmitTicket(),
            _ => CanSubmit);

        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? RequestClose;
    public event Func<System.Drawing.Rectangle?>? RequestRegionSelection;
    public event Func<IList<WindowInfo>, WindowInfo?>? RequestWindowSelection;
    public event Action? RequestHideWindow;
    public event Action? RequestShowWindow;

    public BitmapImage? ScreenshotPreview
    {
        get => _screenshotPreview;
        set => SetField(ref _screenshotPreview, value);
    }

    public string Description
    {
        get => _description;
        set
        {
            SetField(ref _description, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ScreenshotMode SelectedMode
    {
        get => _selectedMode;
        set => SetField(ref _selectedMode, value);
    }

    public bool IsRegionMode
    {
        get => SelectedMode == ScreenshotMode.Region;
        set { if (value) SelectedMode = ScreenshotMode.Region; }
    }

    public bool IsFullScreenMode
    {
        get => SelectedMode == ScreenshotMode.FullScreen;
        set { if (value) SelectedMode = ScreenshotMode.FullScreen; }
    }

    public bool IsWindowMode
    {
        get => SelectedMode == ScreenshotMode.Window;
        set { if (value) SelectedMode = ScreenshotMode.Window; }
    }

    public global::System.Windows.Visibility NoScreenshotVisibility =>
        HasScreenshot ? global::System.Windows.Visibility.Collapsed : global::System.Windows.Visibility.Visible;

    public SystemInfo SystemInfo
    {
        get => _systemInfo;
        set => SetField(ref _systemInfo, value);
    }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        set
        {
            SetField(ref _isSubmitting, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool HasScreenshot => _currentScreenshot != null;
    public bool CanSubmit => HasScreenshot && !string.IsNullOrWhiteSpace(Description) && !IsSubmitting;

    public ICommand CaptureScreenshotCommand { get; }
    public ICommand SubmitTicketCommand { get; }
    public ICommand CancelCommand { get; }

    public string CurrentUsername => Environment.UserName;

    public bool IsConnected => _agentConfig?.IsValid == true;

    public string ConnectionStatus => IsConnected
        ? $"Verbunden mit {_agentConfig!.ApiUrl}"
        : "Offline-Modus (keine Agent-Konfiguration gefunden)";

    public string SystemInfoSummary =>
        $"Benutzer: {SystemInfo.Username}  |  Hostname: {SystemInfo.Hostname}\n" +
        $"OS: {SystemInfo.OsVersion}\n" +
        $"Domain: {SystemInfo.DomainName}";

    private void CaptureScreenshot()
    {
        try
        {
            _currentScreenshot?.Dispose();
            _currentScreenshot = null;

            RequestHideWindow?.Invoke();

            Thread.Sleep(300);

            switch (SelectedMode)
            {
                case ScreenshotMode.FullScreen:
                    _currentScreenshot = _screenshotService.CaptureFullScreen();
                    break;

                case ScreenshotMode.Region:
                    var region = RequestRegionSelection?.Invoke();
                    if (region.HasValue && region.Value.Width > 0 && region.Value.Height > 0)
                    {
                        _currentScreenshot = _screenshotService.CaptureRegion(region.Value);
                    }
                    break;

                case ScreenshotMode.Window:
                    var windows = _screenshotService.GetVisibleWindows();
                    var selected = RequestWindowSelection?.Invoke(windows);
                    if (selected != null)
                    {
                        _currentScreenshot = _screenshotService.CaptureWindow(selected.Handle);
                    }
                    break;
            }

            if (_currentScreenshot != null)
            {
                ScreenshotPreview = BitmapToImageSourceConverter.Convert(_currentScreenshot);
                OnPropertyChanged(nameof(HasScreenshot));
                OnPropertyChanged(nameof(NoScreenshotVisibility));
                StatusMessage = "Screenshot aufgenommen.";
            }
            else
            {
                StatusMessage = "Screenshot abgebrochen.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
        }
        finally
        {
            RequestShowWindow?.Invoke();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task SubmitTicket()
    {
        if (_currentScreenshot == null) return;

        IsSubmitting = true;
        StatusMessage = "Ticket wird erstellt...";

        try
        {
            var ticket = new Ticket
            {
                Description = Description,
                CaptureMode = SelectedMode,
                SystemInfo = _systemInfoService.Collect()
            };

            var pngBytes = BitmapToImageSourceConverter.ToByteArray(_currentScreenshot);
            var result = await _ticketService.SubmitTicketAsync(ticket, pngBytes);

            if (result.Submitted && result.TicketNumber != null)
            {
                StatusMessage = $"Ticket gesendet: {result.TicketNumber}";
                System.Windows.MessageBox.Show(
                    $"Ticket wurde erfolgreich erstellt!\n\nTicket-Nr: {result.TicketNumber}\nBenutzer: {ticket.Username}",
                    "Ticket erstellt",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"Ticket lokal gespeichert: {ticket.Id}";
                System.Windows.MessageBox.Show(
                    $"Ticket wurde lokal gespeichert.\n\nDie Verbindung zum Server konnte nicht hergestellt werden.\nDas Ticket wird beim naechsten Mal erneut versucht.\n\nPfad: {result.LocalPath}",
                    "Ticket lokal gespeichert",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }

            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Fehler beim Erstellen des Tickets:\n{ex.Message}",
                "Fehler",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
