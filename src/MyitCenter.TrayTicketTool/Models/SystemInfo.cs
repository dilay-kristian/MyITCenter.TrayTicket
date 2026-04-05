namespace MyitCenter.TrayTicketTool.Models;

public class SystemInfo
{
    public string Hostname { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; }
}
