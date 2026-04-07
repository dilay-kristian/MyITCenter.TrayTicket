namespace MyitCenter.TrayTicketTool.Models;

public class TicketMessage
{
    public int Id { get; set; }
    public string From { get; set; } = string.Empty;  // "customer" or "agent"
    public string Author { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<TicketAttachment> Attachments { get; set; } = new();
}

public class TicketAttachment
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
