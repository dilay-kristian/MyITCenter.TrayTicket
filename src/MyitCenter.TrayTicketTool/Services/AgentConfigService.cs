using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public class AgentConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ITDokuAgent", "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AgentConfig? Load()
    {
        if (!File.Exists(ConfigPath))
            return null;

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<AgentConfig>(json, JsonOptions);
    }
}
