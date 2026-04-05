namespace MyitCenter.TrayTicketTool.Models;

public class Ticket
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Username { get; set; } = Environment.UserName;
    public string Description { get; set; } = string.Empty;
    public string ScreenshotPath { get; set; } = string.Empty;
    public ScreenshotMode CaptureMode { get; set; }
    public SystemInfo SystemInfo { get; set; } = new();
    public int? DeviceId { get; set; }
    public string Status { get; set; } = "local";
}
