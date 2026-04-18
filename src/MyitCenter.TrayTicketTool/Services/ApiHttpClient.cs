using System.Net.Http;
using System.Net.Http.Headers;
using MyitCenter.TrayTicketTool.Models;

namespace MyitCenter.TrayTicketTool.Services;

/// <summary>
/// Zentraler HttpClient fuer alle API-Aufrufe.
/// Shared Instance (kein Socket-Leak), Timeout, Retry-Logik.
/// </summary>
public sealed class ApiHttpClient : IDisposable
{
    private static ApiHttpClient? _instance;
    private readonly HttpClient _client;

    public bool IsOnline { get; private set; } = true;
    public event Action<bool>? ConnectionStatusChanged;

    private ApiHttpClient(AgentConfig config)
    {
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.AgentToken);
    }

    public static ApiHttpClient GetInstance(AgentConfig config)
    {
        _instance ??= new ApiHttpClient(config);
        return _instance;
    }

    public async Task<HttpResponseMessage> GetAsync(string url)
    {
        return await SendWithRetryAsync(() => new HttpRequestMessage(HttpMethod.Get, url));
    }

    public async Task<HttpResponseMessage> PostAsync(string url, HttpContent content)
    {
        return await SendWithRetryAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            return request;
        }, maxRetries: 1); // POST nur 1x retry
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        int maxRetries = 2)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    LogService.Info($"Retry {attempt}/{maxRetries} nach {delay.TotalSeconds}s...");
                    await Task.Delay(delay);
                }

                var request = requestFactory();
                var response = await _client.SendAsync(request);

                SetOnline(true);
                return response;
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                LogService.Warn($"HTTP-Fehler (Versuch {attempt + 1}/{maxRetries + 1}): {ex.Message}");
                SetOnline(false);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                lastException = ex;
                LogService.Warn($"Timeout (Versuch {attempt + 1}/{maxRetries + 1})");
                SetOnline(false);
            }
        }

        throw lastException ?? new HttpRequestException("Unbekannter Fehler");
    }

    private void SetOnline(bool online)
    {
        if (IsOnline != online)
        {
            IsOnline = online;
            ConnectionStatusChanged?.Invoke(online);
            LogService.Info($"Verbindungsstatus: {(online ? "Online" : "Offline")}");
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _instance = null;
    }
}
