using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyitCenter.TrayTicketTool.Helpers;
using MyitCenter.TrayTicketTool.Services;
using WinForms = System.Windows.Forms;

namespace MyitCenter.TrayTicketTool.Views;

public partial class RegionSelectionWindow : Window
{
    private System.Windows.Point _startPoint;
    private bool _isDragging;
    private Bitmap? _frozenScreen;
    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;

    public Rect? SelectedRegion { get; private set; }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    public RegionSelectionWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // DPI-Skalierung ermitteln
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            _dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            _dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
        }

        LogService.Info($"RegionSelection DPI Scale: {_dpiScaleX}x{_dpiScaleY}");

        // Physische Pixel des virtuellen Screens
        var screen = WinForms.SystemInformation.VirtualScreen;

        // WPF-Koordinaten (logische Pixel)
        double wpfLeft = screen.Left / _dpiScaleX;
        double wpfTop = screen.Top / _dpiScaleY;
        double wpfWidth = screen.Width / _dpiScaleX;
        double wpfHeight = screen.Height / _dpiScaleY;

        Left = wpfLeft;
        Top = wpfTop;
        Width = wpfWidth;
        Height = wpfHeight;

        OverlayCanvas.Width = wpfWidth;
        OverlayCanvas.Height = wpfHeight;

        // Screenshot aufnehmen (physische Pixel)
        var screenshotService = new ScreenshotService();
        _frozenScreen = screenshotService.CaptureFullScreen();

        var bitmapSource = BitmapToImageSourceConverter.Convert(_frozenScreen);
        BackgroundImage.Source = bitmapSource;
        BackgroundImage.Width = wpfWidth;
        BackgroundImage.Height = wpfHeight;
        Canvas.SetLeft(BackgroundImage, 0);
        Canvas.SetTop(BackgroundImage, 0);

        UpdateOverlay(new Rect(0, 0, 0, 0));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(OverlayCanvas);
        _isDragging = true;
        SelectionRect.Visibility = Visibility.Visible;
        SizeIndicator.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = e.GetPosition(OverlayCanvas);
        var rect = GetSelectionRect(_startPoint, current);

        Canvas.SetLeft(SelectionRect, rect.X);
        Canvas.SetTop(SelectionRect, rect.Y);
        SelectionRect.Width = rect.Width;
        SelectionRect.Height = rect.Height;

        // Groesse in physischen Pixeln anzeigen
        var physW = (int)(rect.Width * _dpiScaleX);
        var physH = (int)(rect.Height * _dpiScaleY);
        SizeText.Text = $"{physW} x {physH}";
        Canvas.SetLeft(SizeIndicator, rect.X);
        Canvas.SetTop(SizeIndicator, rect.Y - 25);

        UpdateOverlay(rect);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();

        var current = e.GetPosition(OverlayCanvas);
        var rect = GetSelectionRect(_startPoint, current);

        if (rect.Width > 5 && rect.Height > 5)
        {
            // WPF logische Koordinaten → physische Screen-Koordinaten
            var screen = WinForms.SystemInformation.VirtualScreen;
            SelectedRegion = new Rect(
                rect.X * _dpiScaleX + screen.Left,
                rect.Y * _dpiScaleY + screen.Top,
                rect.Width * _dpiScaleX,
                rect.Height * _dpiScaleY);

            LogService.Info($"Region ausgewaehlt: {SelectedRegion} (physisch)");
            DialogResult = true;
        }

        _frozenScreen?.Dispose();
        Close();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _frozenScreen?.Dispose();
            DialogResult = false;
            Close();
        }
    }

    private Rect GetSelectionRect(System.Windows.Point start, System.Windows.Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var w = Math.Abs(end.X - start.X);
        var h = Math.Abs(end.Y - start.Y);
        return new Rect(x, y, w, h);
    }

    private void UpdateOverlay(Rect selection)
    {
        var fullRect = new RectangleGeometry(new Rect(0, 0, OverlayCanvas.Width, OverlayCanvas.Height));

        if (selection.Width > 0 && selection.Height > 0)
        {
            var selectionGeometry = new RectangleGeometry(selection);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, selectionGeometry);
            DarkOverlay.Data = combined;
        }
        else
        {
            DarkOverlay.Data = fullRect;
        }
    }
}
