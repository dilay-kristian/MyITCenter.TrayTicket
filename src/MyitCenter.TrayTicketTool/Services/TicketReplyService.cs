using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public class TicketReplyService
{
    private readonly AgentConfig _config;

    public TicketReplyService(AgentConfig config)
    {
        _config = config;
    }

    public async Task<bool> SendReplyAsync(int ticketId, string? message, byte[]? screenshotPng, List<string>? filePaths = null)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.AgentToken);

        var url = $"{_config.ApiUrl.TrimEnd('/')}/api/agent/ticket/{ticketId}/reply";

        LogService.Info($"Reply senden: POST {url}");

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

        if (filePaths != null)
        {
            foreach (var filePath in filePaths)
            {
                if (!File.Exists(filePath)) continue;

                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "attachments[]", Path.GetFileName(filePath));

                LogService.Info($"Anhang: {Path.GetFileName(filePath)} ({fileBytes.Length} Bytes)");
            }
        }

        var response = await client.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        LogService.Info($"Reply-Antwort: HTTP {(int)response.StatusCode} — {responseBody}");

        response.EnsureSuccessStatusCode();
        return true;
    }
}
