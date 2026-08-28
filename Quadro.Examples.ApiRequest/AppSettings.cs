using System.Text.Json;

namespace QuadroApiExample;

public sealed class AppSettings
{
    public OAuthSettings OAuth { get; init; } = new();
    public ApiRequestSettings Api { get; init; } = new();

    public static async Task<AppSettings> LoadAsync(
        string fileName = "appsettings.json",
        CancellationToken cancellationToken = default)
    {
        ConsoleLogger.Info($"Loading configuration from '{fileName}'.");

        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException(
                $"Configuration file '{fileName}' was not found. Copy appsettings.example.json to appsettings.json first.");
        }

        await using var stream = File.OpenRead(fileName);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
            stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            },
            cancellationToken);

        if (settings is null)
        {
            throw new InvalidOperationException($"Could not read configuration from '{fileName}'.");
        }

        settings.ApplyEnvironmentOverrides();
        settings.Validate();

        ConsoleLogger.Success("Configuration loaded and validated successfully.");
        return settings;
    }

    private void ApplyEnvironmentOverrides()
    {
        OAuth.AuthorizationUrl = GetEnvironmentOrCurrent("QUADRO_OAUTH_AUTH_URL", OAuth.AuthorizationUrl);
        OAuth.TokenUrl = GetEnvironmentOrCurrent("QUADRO_OAUTH_TOKEN_URL", OAuth.TokenUrl);
        OAuth.ClientId = GetEnvironmentOrCurrent("QUADRO_OAUTH_CLIENT_ID", OAuth.ClientId);
        OAuth.ClientSecret = GetEnvironmentOrCurrent("QUADRO_OAUTH_CLIENT_SECRET", OAuth.ClientSecret);
        OAuth.Audience = GetEnvironmentOrCurrent("QUADRO_OAUTH_AUDIENCE", OAuth.Audience);
        OAuth.RedirectUri = GetEnvironmentOrCurrent("QUADRO_OAUTH_REDIRECT_URI", OAuth.RedirectUri);

        Api.RequestUrl = GetEnvironmentOrCurrent("QUADRO_API_URL", Api.RequestUrl);
        Api.Method = GetEnvironmentOrCurrent("QUADRO_API_METHOD", Api.Method);
        Api.JsonBody = GetEnvironmentOrCurrent("QUADRO_API_BODY", Api.JsonBody);
    }

    private static string? GetEnvironmentOrCurrent(string name, string? current)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? current : value;
    }

    private void Validate()
    {
        RequireAbsoluteUri(OAuth.AuthorizationUrl, "OAuth.AuthorizationUrl");
        RequireAbsoluteUri(OAuth.TokenUrl, "OAuth.TokenUrl");
        RequireAbsoluteUri(OAuth.RedirectUri, "OAuth.RedirectUri");
        RequireAbsoluteUri(Api.RequestUrl, "Api.RequestUrl");

        if (string.IsNullOrWhiteSpace(OAuth.ClientId))
        {
            throw new InvalidOperationException("OAuth.ClientId is required.");
        }

        if (string.IsNullOrWhiteSpace(Api.Method))
        {
            throw new InvalidOperationException("Api.Method is required.");
        }

        var redirectUri = new Uri(OAuth.RedirectUri!);
        if (redirectUri.Scheme != Uri.UriSchemeHttp || !redirectUri.IsLoopback)
        {
            throw new InvalidOperationException(
                "For this console example, OAuth.RedirectUri must be a local HTTP loopback URL, " +
                "for example http://127.0.0.1:53682/callback.");
        }
    }

    private static void RequireAbsoluteUri(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"{name} must be an absolute URL.");
        }
    }
}

public sealed class OAuthSettings
{
    public string? AuthorizationUrl { get; set; }
    public string? TokenUrl { get; set; }
    public string? ClientId { get; set; }

    // Optional. When configured, the client secret is sent in the token request body together with client_id.
    public string? ClientSecret { get; set; }

    public string? Audience { get; set; }
    public string? RedirectUri { get; set; } = "http://127.0.0.1:53682/callback";
    public int BrowserTimeoutSeconds { get; set; } = 300;
}

public sealed class ApiRequestSettings
{
    public string? RequestUrl { get; set; }
    public string? Method { get; set; } = "GET";
    public string? JsonBody { get; set; }
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
