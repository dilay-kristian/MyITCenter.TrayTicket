using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public class ApiTicketService : ITicketService
{
    private readonly AgentConfig _config;
    private readonly LocalTicketService _localFallback = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public ApiTicketService(AgentConfig config)
    {
        _config = config;
    }

    public async Task<TicketResult> SubmitTicketAsync(Ticket ticket, byte[] screenshotPng)
    {
        ticket.DeviceId = _config.DeviceId;

        LogService.Info($"Ticket erstellen: id={ticket.Id}, user={ticket.Username}, device_id={ticket.DeviceId}");

        // Lokal speichern als Backup
        var localResult = await _localFallback.SubmitTicketAsync(ticket, screenshotPng);
        LogService.Info($"Lokales Backup gespeichert: {localResult.LocalPath}");

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.AgentToken);

            var url = $"{_config.ApiUrl.TrimEnd('/')}/api/agent/ticket";
            LogService.Info($"API-Aufruf: POST {url}");

            using var content = new MultipartFormDataContent();

            var ticketJson = JsonSerializer.Serialize(ticket, JsonOptions);
            content.Add(new StringContent(ticketJson, Encoding.UTF8, "application/json"), "ticket");

            var screenshotContent = new ByteArrayContent(screenshotPng);
            screenshotContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(screenshotContent, "screenshot", $"{ticket.Id}.png");

            LogService.Info($"Sende Ticket (JSON: {ticketJson.Length} Bytes, Screenshot: {screenshotPng.Length} Bytes)");

            var response = await client.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            LogService.Info($"API-Antwort: HTTP {(int)response.StatusCode} — {responseJson}");

            if (!response.IsSuccessStatusCode)
            {
                LogService.Error($"API-Fehler: HTTP {(int)response.StatusCode} — {responseJson}");
                ticket.Status = "local";
                return localResult;
            }

            var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

            string? ticketNumber = null;
            if (apiResponse.TryGetProperty("ticket_number", out var tn))
                ticketNumber = tn.GetString();

            ticket.Status = "submitted";
            LogService.Info($"Ticket erfolgreich gesendet: {ticketNumber}");

            return new TicketResult
            {
                Submitted = true,
                TicketNumber = ticketNumber,
                LocalPath = localResult.LocalPath
            };
        }
        catch (HttpRequestException ex)
        {
            LogService.Error("API nicht erreichbar", ex);
            ticket.Status = "local";
            return localResult;
        }
        catch (Exception ex)
        {
            LogService.Error("Unerwarteter Fehler beim Ticket-Upload", ex);
            ticket.Status = "local";
            return localResult;
        }
    }
}
