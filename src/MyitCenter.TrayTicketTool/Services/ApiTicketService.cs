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

        // Lokal speichern als Backup
        var localResult = await _localFallback.SubmitTicketAsync(ticket, screenshotPng);

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.AgentToken);

            var url = $"{_config.ApiUrl.TrimEnd('/')}/api/agent/ticket";

            using var content = new MultipartFormDataContent();

            // Ticket-Daten als JSON
            var ticketJson = JsonSerializer.Serialize(ticket, JsonOptions);
            content.Add(new StringContent(ticketJson, Encoding.UTF8, "application/json"), "ticket");

            // Screenshot als Datei
            var screenshotContent = new ByteArrayContent(screenshotPng);
            screenshotContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(screenshotContent, "screenshot", $"{ticket.Id}.png");

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            // API Response auswerten: {"success":true,"ticket_number":"TKT-2026-00175","ticket_id":230}
            var responseJson = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);

            string? ticketNumber = null;
            if (apiResponse.TryGetProperty("ticket_number", out var tn))
                ticketNumber = tn.GetString();

            ticket.Status = "submitted";

            return new TicketResult
            {
                Submitted = true,
                TicketNumber = ticketNumber,
                LocalPath = localResult.LocalPath
            };
        }
        catch (HttpRequestException)
        {
            // API nicht erreichbar — lokales Backup bleibt bestehen
            ticket.Status = "local";
            return localResult;
        }
    }
}
