using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuadroApiExample;

public sealed class PkceOAuthClient : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly OAuthSettings _settings;

    public PkceOAuthClient(OAuthSettings settings)
    {
        _settings = settings;
    }

    public async Task<OAuthTokenResponse> LoginAsync(CancellationToken cancellationToken = default)
    {
        ConsoleLogger.Info("Starting OAuth 2.0 Authorization Code flow with PKCE (S256).");

        var codeVerifier = CreateCodeVerifier();
        var codeChallenge = CreateCodeChallenge(codeVerifier);
        var redirectUri = new Uri(_settings.RedirectUri!);

        ConsoleLogger.Info("PKCE code verifier and SHA-256 code challenge generated.");

        using var listener = CreateLoopbackListener(redirectUri);
        listener.Start();
        ConsoleLogger.Success($"Local OAuth callback listener started on {redirectUri}.");

        var authorizationUri = BuildAuthorizationUri(codeChallenge);
        ConsoleLogger.Info($"Authorization endpoint: {_settings.AuthorizationUrl}");
        ConsoleLogger.Info($"Opening the system browser for authentication: {authorizationUri}");

        if (OpenBrowser(authorizationUri))
        {
            ConsoleLogger.Success("System browser opened successfully.");
        }
        else
        {
            ConsoleLogger.Warning("The browser could not be opened automatically. Open the authorization URL from the previous log entry manually.");
        }

        ConsoleLogger.Info($"Waiting for the OAuth callback for up to {_settings.BrowserTimeoutSeconds} seconds.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.BrowserTimeoutSeconds));

        HttpListenerContext context;
        try
        {
            context = await WaitForCallbackAsync(listener, redirectUri, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No OAuth callback was received within {_settings.BrowserTimeoutSeconds} seconds.");
        }

        ConsoleLogger.Success("OAuth callback received on the expected redirect path.");

        var request = context.Request;
        var error = request.QueryString["error"];
        var errorDescription = request.QueryString["error_description"];
        var code = request.QueryString["code"];

        if (!string.IsNullOrWhiteSpace(error))
        {
            await WriteBrowserResponseAsync(
                context.Response,
                "OAuth login failed. You can close this browser window.");

            throw new InvalidOperationException(
                $"OAuth authorization error: {error}" +
                (string.IsNullOrWhiteSpace(errorDescription) ? string.Empty : $" - {errorDescription}"));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            await WriteBrowserResponseAsync(
                context.Response,
                "No authorization code was received. You can close this browser window.");

            throw new InvalidOperationException("The OAuth callback did not contain a 'code' parameter.");
        }

        ConsoleLogger.Success("Authorization code received. The code itself is intentionally not logged.");

        await WriteBrowserResponseAsync(
            context.Response,
            "Authentication succeeded. You can close this browser window and return to the application.");

        return await ExchangeCodeForTokenAsync(code, codeVerifier, cancellationToken);
    }

    private Uri BuildAuthorizationUri(string codeChallenge)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _settings.ClientId,
            ["redirect_uri"] = _settings.RedirectUri,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        // Postman uses an extra authorization request parameter named "audience".
        if (!string.IsNullOrWhiteSpace(_settings.Audience))
        {
            parameters["audience"] = _settings.Audience;
        }

        // Scope and state are intentionally omitted to match the currently working Postman configuration.
        return AppendQueryParameters(new Uri(_settings.AuthorizationUrl!), parameters);
    }

    private async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(
        string authorizationCode,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["redirect_uri"] = _settings.RedirectUri!,
            ["client_id"] = _settings.ClientId!,
            ["code_verifier"] = codeVerifier
        };

        // "Send client credentials in body": client_id is always included in the form body.
        // A client secret is also sent in the body when one has been configured.
        if (!string.IsNullOrWhiteSpace(_settings.ClientSecret))
        {
            form["client_secret"] = _settings.ClientSecret;
            ConsoleLogger.Info("A client secret is configured and will be sent in the token request body. The secret itself is not logged.");
        }
        else
        {
            ConsoleLogger.Info("No client secret is configured; the token request will contain client_id only.");
        }

        ConsoleLogger.Info($"Exchanging the authorization code for an access token at {_settings.TokenUrl}.");

        using var response = await _httpClient.PostAsync(
            _settings.TokenUrl,
            new FormUrlEncodedContent(form),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ConsoleLogger.Info($"Token endpoint returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");

        if (!response.IsSuccessStatusCode)
        {
            ConsoleLogger.Warning("The token endpoint returned a non-success status code.");
            throw new OAuthTokenException(
                (int)response.StatusCode,
                response.ReasonPhrase,
                body);
        }

        OAuthTokenResponse? token;
        try
        {
            token = JsonSerializer.Deserialize<OAuthTokenResponse>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            throw new OAuthTokenException(
                (int)response.StatusCode,
                "The token endpoint did not return valid JSON.",
                body,
                ex);
        }

        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new OAuthTokenException(
                (int)response.StatusCode,
                "The token endpoint did not return an access_token.",
                body);
        }

        ConsoleLogger.Success("Access token received successfully. The token value is intentionally not logged.");
        return token;
    }

    private static HttpListener CreateLoopbackListener(Uri redirectUri)
    {
        var listener = new HttpListener();
        var port = redirectUri.IsDefaultPort
            ? (redirectUri.Scheme == Uri.UriSchemeHttps ? 443 : 80)
            : redirectUri.Port;

        // Listen on the authority root so callback paths with or without a trailing slash can be handled safely.
        listener.Prefixes.Add($"http://{redirectUri.Host}:{port}/");
        return listener;
    }

    private static async Task<HttpListenerContext> WaitForCallbackAsync(
        HttpListener listener,
        Uri redirectUri,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            var receivedPath = context.Request.Url?.AbsolutePath;

            if (string.Equals(receivedPath, redirectUri.AbsolutePath, StringComparison.Ordinal))
            {
                return context;
            }

            ConsoleLogger.Warning($"Ignoring callback request for unexpected path '{receivedPath ?? "<unknown>"}'.");
            context.Response.StatusCode = 404;
            context.Response.Close();
        }
    }

    private static async Task WriteBrowserResponseAsync(HttpListenerResponse response, string message)
    {
        var html = $"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><title>Quadro API OAuth</title></head>
            <body style="font-family: sans-serif; margin: 3rem;">
                <h2>{WebUtility.HtmlEncode(message)}</h2>
            </body>
            </html>
            """;

        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static bool OpenBrowser(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });

            return true;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Warning($"Could not open the browser automatically: {ex.Message}");
            return false;
        }
    }

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static Uri AppendQueryParameters(Uri uri, IReadOnlyDictionary<string, string?> parameters)
    {
        var builder = new UriBuilder(uri);
        var queryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(builder.Query))
        {
            queryParts.Add(builder.Query.TrimStart('?'));
        }

        foreach (var (key, value) in parameters)
        {
            if (value is null)
            {
                continue;
            }

            queryParts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        builder.Query = string.Join("&", queryParts);
        return builder.Uri;
    }

    public void Dispose() => _httpClient.Dispose();
}

public sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public long? ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

public sealed class OAuthTokenException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public OAuthTokenException(
        int statusCode,
        string? message,
        string? responseBody,
        Exception? innerException = null)
        : base($"OAuth token request failed ({statusCode}): {message}", innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
