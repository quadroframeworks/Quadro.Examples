using System.Net.Http.Headers;
using System.Text;

namespace QuadroApiExample;

public sealed class QuadroApiClient : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public async Task<ApiResponse> SendAsync(
        ApiRequestSettings settings,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var method = new HttpMethod(settings.Method!.Trim().ToUpperInvariant());
        using var request = new HttpRequestMessage(method, settings.RequestUrl);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(settings.JsonBody))
        {
            request.Content = new StringContent(
                settings.JsonBody,
                Encoding.UTF8,
                "application/json");

            ConsoleLogger.Info("A JSON request body is configured.");
        }

        foreach (var (name, value) in settings.Headers)
        {
            if (!request.Headers.TryAddWithoutValidation(name, value) && request.Content is not null)
            {
                request.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (settings.Headers.Count > 0)
        {
            ConsoleLogger.Info($"Added {settings.Headers.Count} configured custom header(s).");
        }

        ConsoleLogger.Info($"Sending API request: {method.Method} {settings.RequestUrl}");
        ConsoleLogger.Info("Authorization: Bearer <access token hidden>");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            ConsoleLogger.Success($"API request completed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        else
        {
            ConsoleLogger.Warning($"API request completed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return new ApiResponse(
            (int)response.StatusCode,
            response.ReasonPhrase,
            body,
            response.IsSuccessStatusCode);
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed record ApiResponse(
    int StatusCode,
    string? ReasonPhrase,
    string Body,
    bool IsSuccessStatusCode);
