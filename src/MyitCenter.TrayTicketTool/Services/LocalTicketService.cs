using System.IO;
using System.Text.Json;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public class LocalTicketService : ITicketService
{
    private static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyitCenter", "Tickets");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<string> SubmitTicketAsync(Ticket ticket, byte[] screenshotPng)
    {
        var ticketDir = Path.Combine(BaseDir, ticket.Id);
        Directory.CreateDirectory(ticketDir);

        var screenshotPath = Path.Combine(ticketDir, "screenshot.png");
        await File.WriteAllBytesAsync(screenshotPath, screenshotPng);

        ticket.ScreenshotPath = "screenshot.png";
        ticket.Status = "local";

        var jsonPath = Path.Combine(ticketDir, "ticket.json");
        var json = JsonSerializer.Serialize(ticket, JsonOptions);
        await File.WriteAllTextAsync(jsonPath, json);

        return ticketDir;
    }
}
