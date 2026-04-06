using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

public class TicketStatusService : IDisposable
{
    private readonly AgentConfig _config;
    private readonly HttpClient _client;
    private readonly System.Timers.Timer _pollTimer;
    private List<TicketStatusInfo> _lastTickets = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public event Action<List<TicketStatusInfo>>? TicketsUpdated;
    public event Action<TicketStatusInfo>? TicketStatusChanged;

    public List<TicketStatusInfo> CurrentTickets => _lastTickets;

    public TicketStatusService(AgentConfig config, int pollIntervalSeconds = 180)
    {
        _config = config;
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.AgentToken);

        _pollTimer = new System.Timers.Timer(pollIntervalSeconds * 1000);
        _pollTimer.Elapsed += async (_, _) => await PollAsync();
        _pollTimer.AutoReset = true;
    }

    public async Task StartAsync()
    {
        await PollAsync();
        _pollTimer.Start();
    }

    public void Stop()
    {
        _pollTimer.Stop();
    }

    public async Task PollAsync()
    {
        try
        {
            var username = Environment.UserName;
            var url = $"{_config.ApiUrl.TrimEnd('/')}/api/agent/tickets?username={Uri.EscapeDataString(username)}&status=open";

            var response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

            var tickets = new List<TicketStatusInfo>();

            if (doc.TryGetProperty("tickets", out var ticketsArray))
            {
                foreach (var item in ticketsArray.EnumerateArray())
                {
                    tickets.Add(new TicketStatusInfo
                    {
                        TicketId = item.TryGetProperty("ticket_id", out var tid) ? tid.GetInt32() : 0,
                        TicketNumber = item.TryGetProperty("ticket_number", out var tn) ? tn.GetString() ?? "" : "",
                        Subject = item.TryGetProperty("subject", out var sub) ? sub.GetString() ?? "" : "",
                        Status = item.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                        StatusLabel = item.TryGetProperty("status_label", out var sl) ? sl.GetString() ?? "" : "",
                        CreatedAt = item.TryGetProperty("created_at", out var ca) && ca.TryGetDateTime(out var caVal) ? caVal : DateTime.MinValue,
                        UpdatedAt = item.TryGetProperty("updated_at", out var ua) && ua.TryGetDateTime(out var uaVal) ? uaVal : DateTime.MinValue,
                        HasAgentReply = item.TryGetProperty("has_agent_reply", out var har) && har.GetBoolean()
                    });
                }
            }

            // Pruefen ob sich Status geaendert hat
            foreach (var ticket in tickets)
            {
                var old = _lastTickets.Find(t => t.TicketId == ticket.TicketId);
                if (old != null && (old.Status != ticket.Status || old.HasAgentReply != ticket.HasAgentReply))
                {
                    TicketStatusChanged?.Invoke(ticket);
                }
            }

            // Neue Tickets die vorher nicht da waren (z.B. von anderem Tool erstellt)
            foreach (var ticket in tickets)
            {
                if (!_lastTickets.Exists(t => t.TicketId == ticket.TicketId))
                {
                    // Nur benachrichtigen wenn es nicht der allererste Poll ist
                    if (_lastTickets.Count > 0)
                        TicketStatusChanged?.Invoke(ticket);
                }
            }

            _lastTickets = tickets;
            TicketsUpdated?.Invoke(tickets);
        }
        catch (Exception)
        {
            // Netzwerkfehler still ignorieren — naechster Poll versucht es erneut
        }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
        _client.Dispose();
    }
}
