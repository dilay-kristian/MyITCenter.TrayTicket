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
        try
        {
            LogService.Info($"Config-Pfad: {ConfigPath}");

            if (!File.Exists(ConfigPath))
            {
                LogService.Warn($"Config-Datei nicht gefunden: {ConfigPath}");
                return null;
            }

            var json = File.ReadAllText(ConfigPath);
            LogService.Info($"Config-Datei gelesen ({json.Length} Zeichen)");

            var config = JsonSerializer.Deserialize<AgentConfig>(json, JsonOptions);
            if (config == null)
            {
                LogService.Error("Config-Deserialisierung ergab null");
                return null;
            }

            LogService.Info($"Config geparst: device_id={config.DeviceId}, api_url={config.ApiUrl}, enable_ticket_status={config.EnableTicketStatus}, IsValid={config.IsValid}");
            return config;
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler beim Laden der Agent-Config", ex);
            return null;
        }
    }
}
