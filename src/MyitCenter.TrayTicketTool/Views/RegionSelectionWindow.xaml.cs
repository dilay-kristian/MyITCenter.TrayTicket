using System.Drawing;
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

    public Rect? SelectedRegion { get; private set; }

    public RegionSelectionWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Span across all monitors
        var screen = WinForms.SystemInformation.VirtualScreen;
        Left = screen.Left;
        Top = screen.Top;
        Width = screen.Width;
        Height = screen.Height;

        OverlayCanvas.Width = screen.Width;
        OverlayCanvas.Height = screen.Height;

        // Capture the frozen screenshot
        var screenshotService = new ScreenshotService();
        _frozenScreen = screenshotService.CaptureFullScreen();

        var bitmapSource = BitmapToImageSourceConverter.Convert(_frozenScreen);
        BackgroundImage.Source = bitmapSource;
        BackgroundImage.Width = screen.Width;
        BackgroundImage.Height = screen.Height;
        Canvas.SetLeft(BackgroundImage, 0);
        Canvas.SetTop(BackgroundImage, 0);

        // Initial dark overlay covers everything
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

        // Update size indicator
        SizeText.Text = $"{(int)rect.Width} x {(int)rect.Height}";
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
            // Convert to screen coordinates
            var screen = WinForms.SystemInformation.VirtualScreen;
            SelectedRegion = new Rect(
                rect.X + screen.Left,
                rect.Y + screen.Top,
                rect.Width,
                rect.Height);
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
