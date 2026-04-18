using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SharpAvi;
using SharpAvi.Codecs;
using SharpAvi.Output;

namespace MyitCenter.TrayTicketTool.Services;

public class ScreenRecordingService : IDisposable
{
    private AviWriter? _writer;
    private IAviVideoStream? _videoStream;
    private Thread? _captureThread;
    private volatile bool _recording;

    private static readonly string RecordingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyitCenter", "ScreenRecordings");

    public bool IsRecording => _recording;
    public string? CurrentFilePath { get; private set; }
    public DateTime? StartedAt { get; private set; }

    public event Action<string>? RecordingCompleted;
    public event Action<string>? RecordingFailed;

    public string StartRecording(int fps = 10)
    {
        if (_recording)
            throw new InvalidOperationException("Aufnahme läuft bereits.");

        Directory.CreateDirectory(RecordingsDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var fileName = $"ScreenRecording_{timestamp}.avi";
        CurrentFilePath = Path.Combine(RecordingsDir, fileName);

        var screenBounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;

        LogService.Info($"Bildschirmaufnahme: {screenBounds.Width}x{screenBounds.Height} @ {fps}fps -> {CurrentFilePath}");

        try
        {
            _writer = new AviWriter(CurrentFilePath)
            {
                FramesPerSecond = fps,
                EmitIndex1 = true
            };

            _videoStream = _writer.AddMJpegWpfVideoStream(
                screenBounds.Width,
                screenBounds.Height,
                quality: 70);

            _recording = true;
            StartedAt = DateTime.Now;

            _captureThread = new Thread(() => CaptureLoop(screenBounds, fps))
            {
                IsBackground = true,
                Name = "ScreenCapture"
            };
            _captureThread.Start();

            LogService.Info("Bildschirmaufnahme gestartet.");
            return CurrentFilePath;
        }
        catch (Exception ex)
        {
            LogService.Error("Bildschirmaufnahme Start fehlgeschlagen", ex);
            Cleanup();
            throw;
        }
    }

    private const int MaxRecordingMinutes = 10;

    private void CaptureLoop(Rectangle bounds, int fps)
    {
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / fps);
        var maxDuration = TimeSpan.FromMinutes(MaxRecordingMinutes);

        try
        {
            while (_recording)
            {
                // Automatisch stoppen nach Limit
                if (StartedAt.HasValue && DateTime.Now - StartedAt.Value > maxDuration)
                {
                    LogService.Warn($"Bildschirmaufnahme nach {MaxRecordingMinutes} Minuten automatisch gestoppt.");
                    _recording = false;
                    break;
                }
                var frameStart = DateTime.UtcNow;

                using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                    // Mauszeiger einzeichnen
                    DrawCursor(graphics, bounds);
                }

                // Bitmap -> byte[] fuer SharpAvi
                var bits = bitmap.LockBits(
                    new Rectangle(0, 0, bounds.Width, bounds.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);

                var buffer = new byte[bits.Stride * bits.Height];
                Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);
                bitmap.UnlockBits(bits);

                // Frame schreiben (auf BGRA, was SharpAvi MJPEG erwartet)
                _videoStream?.WriteFrame(true, buffer, 0, buffer.Length);

                // Timing einhalten
                var elapsed = DateTime.UtcNow - frameStart;
                var sleepTime = frameInterval - elapsed;
                if (sleepTime > TimeSpan.Zero)
                    Thread.Sleep(sleepTime);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Bildschirmaufnahme Capture-Loop Fehler", ex);
            _recording = false;
            RecordingFailed?.Invoke(ex.Message);
            return;
        }

        Cleanup();
        LogService.Info($"Bildschirmaufnahme gespeichert: {CurrentFilePath}");
        RecordingCompleted?.Invoke(CurrentFilePath!);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);

    [DllImport("user32.dll")]
    private static extern bool DrawIcon(IntPtr hdc, int x, int y, IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    private static void DrawCursor(Graphics g, Rectangle screenBounds)
    {
        try
        {
            var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
            if (GetCursorInfo(ref ci) && ci.flags == 1) // CURSOR_SHOWING
            {
                var hdc = g.GetHdc();
                DrawIcon(hdc, ci.ptScreenPos.X - screenBounds.X, ci.ptScreenPos.Y - screenBounds.Y, ci.hCursor);
                g.ReleaseHdc(hdc);
            }
        }
        catch
        {
            // Cursor zeichnen ist nicht kritisch
        }
    }

    public void StopRecording()
    {
        if (!_recording) return;

        LogService.Info("Bildschirmaufnahme: Stop angefordert...");
        _recording = false;
        _captureThread?.Join(5000);
    }

    private void Cleanup()
    {
        try
        {
            _writer?.Close();
        }
        catch { }
        _writer = null;
        _videoStream = null;
    }

    public void Dispose()
    {
        if (_recording)
            StopRecording();
        Cleanup();
    }
}
