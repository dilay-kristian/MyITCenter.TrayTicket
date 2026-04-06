namespace MyitCenter.TrayTicketTool.Models;

public class AgentConfig
{
    public int DeviceId { get; set; }
    public string AgentToken { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public bool EnableTicketStatus { get; set; }

    public bool IsValid => DeviceId > 0
        && !string.IsNullOrWhiteSpace(AgentToken)
        && !string.IsNullOrWhiteSpace(ApiUrl);
}
