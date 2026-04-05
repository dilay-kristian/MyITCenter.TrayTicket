using System.Drawing;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public interface IScreenshotService
{
    Bitmap CaptureFullScreen();
    Bitmap CaptureRegion(Rectangle region);
    IList<WindowInfo> GetVisibleWindows();
    Bitmap CaptureWindow(IntPtr handle);
}

public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;

    public override string ToString() => $"{Title} ({ProcessName})";
}
