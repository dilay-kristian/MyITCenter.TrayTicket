using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public class TicketMessagesService
{
    private readonly AgentConfig _config;

    public TicketMessagesService(AgentConfig config)
    {
        _config = config;
    }

    public async Task<(string? Subject, List<TicketMessage> Messages)> GetMessagesAsync(int ticketId)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.AgentToken);

        var username = Environment.UserName;
        var url = $"{_config.ApiUrl.TrimEnd('/')}/api/agent/ticket/{ticketId}/messages?username={Uri.EscapeDataString(username)}";

        LogService.Info($"Lade Nachrichten: GET {url}");

        var response = await client.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();

        LogService.Info($"Messages-Antwort: HTTP {(int)response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            LogService.Error($"Messages-Fehler: {json}");
            return (null, new List<TicketMessage>());
        }

        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var subject = doc.TryGetProperty("subject", out var sub) ? sub.GetString() : null;

        var messages = new List<TicketMessage>();

        if (doc.TryGetProperty("messages", out var messagesArray))
        {
            foreach (var item in messagesArray.EnumerateArray())
            {
                var msg = new TicketMessage
                {
                    Id = item.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                    From = item.TryGetProperty("from", out var from) ? from.GetString() ?? "" : "",
                    Author = item.TryGetProperty("author", out var author) ? author.GetString() ?? "" : "",
                    Body = item.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                    CreatedAt = item.TryGetProperty("created_at", out var ca) && ca.TryGetDateTime(out var caVal) ? caVal : DateTime.MinValue
                };

                if (item.TryGetProperty("attachments", out var attachments))
                {
                    foreach (var att in attachments.EnumerateArray())
                    {
                        msg.Attachments.Add(new TicketAttachment
                        {
                            Name = att.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                            Url = att.TryGetProperty("url", out var attUrl) ? attUrl.GetString() ?? "" : ""
                        });
                    }
                }

                messages.Add(msg);
            }
        }

        return (subject, messages);
    }
}
