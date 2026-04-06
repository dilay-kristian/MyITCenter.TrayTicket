namespace MyitCenter.TrayTicketTool.Models;

public class TicketResult
{
    public bool Submitted { get; set; }
    public string? TicketNumber { get; set; }
    public string LocalPath { get; set; } = string.Empty;
}
