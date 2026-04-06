using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public class TicketReplyService
{
    private readonly AgentConfig _config;

    public TicketReplyService(AgentConfig config)
    {
        _config = config;
    }

    public async Task<bool> SendReplyAsync(int ticketId, string? message, byte[]? screenshotPng)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.AgentToken);

        var url = $"{_config.ApiUrl.TrimEnd('/')}/api/agent/ticket/{ticketId}/reply";

        using var content = new MultipartFormDataContent();

        content.Add(new StringContent(Environment.UserName), "username");

        if (!string.IsNullOrWhiteSpace(message))
            content.Add(new StringContent(message), "message");

        if (screenshotPng != null)
        {
            var screenshotContent = new ByteArrayContent(screenshotPng);
            screenshotContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(screenshotContent, "screenshot", $"reply_{Guid.NewGuid()}.png");
        }

        var response = await client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        return true;
    }
}
