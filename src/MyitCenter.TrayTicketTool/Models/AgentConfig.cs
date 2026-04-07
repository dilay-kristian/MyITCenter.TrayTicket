namespace MyitCenter.TrayTicketTool.Models;

public class AgentConfig
{
    public int DeviceId { get; set; }
    public string AgentToken { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public bool EnableTicketStatus { get; set; }
    public string? MeshcentralUrl { get; set; }
    public string? MeshcentralNodeId { get; set; }

    public string? MeshcentralLink =>
        !string.IsNullOrWhiteSpace(MeshcentralUrl) && !string.IsNullOrWhiteSpace(MeshcentralNodeId)
            ? $"{MeshcentralUrl.TrimEnd('/')}/#/device/{MeshcentralNodeId}"
            : null;

    public bool IsValid => DeviceId > 0
        && !string.IsNullOrWhiteSpace(AgentToken)
        && !string.IsNullOrWhiteSpace(ApiUrl);
}
