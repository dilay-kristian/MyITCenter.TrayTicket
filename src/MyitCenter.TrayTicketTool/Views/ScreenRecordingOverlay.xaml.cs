using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MyitCenter.TrayTicketTool.Views;

public partial class ScreenRecordingOverlay : Window
{
    private readonly DispatcherTimer _timer;
    private readonly DateTime _startTime;

    public event Action? StopRequested;

    public ScreenRecordingOverlay()
    {
        InitializeComponent();

        // Oben rechts positionieren
        var screen = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        Left = screen.Right - Width - 16;
        Top = screen.Top + 16;

        _startTime = DateTime.Now;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _startTime;
            TimerText.Text = $"REC {elapsed:mm\\:ss}";
        };
        _timer.Start();

        // Blinkender Punkt
        var animation = new DoubleAnimation(1, 0.2, TimeSpan.FromMilliseconds(800))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        RecordingDot.BeginAnimation(OpacityProperty, animation);

        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        StopRequested?.Invoke();
    }

    public new void Close()
    {
        _timer.Stop();
        base.Close();
    }
}
