namespace MyitCenter.TrayTicketTool.Models;

public class TicketStatusInfo
{
    public int TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool HasAgentReply { get; set; }
}
