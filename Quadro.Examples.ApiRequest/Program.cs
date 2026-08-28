using System.Text.Json;
using QuadroApiExample;

try
{
    ConsoleLogger.Info("Quadro API example started.");

    var settings = await AppSettings.LoadAsync();

    ConsoleLogger.Info($"Configured API request: {settings.Api.Method!.ToUpperInvariant()} {settings.Api.RequestUrl}");

    using var oauth = new PkceOAuthClient(settings.OAuth);
    var token = await oauth.LoginAsync();

    ConsoleLogger.Success($"OAuth authentication completed. Token type: {token.TokenType ?? "Bearer"}.");

    if (token.ExpiresIn is not null)
    {
        ConsoleLogger.Info($"Access token expires in {token.ExpiresIn} seconds.");
    }
    else
    {
        ConsoleLogger.Warning("The token response did not contain an expires_in value.");
    }

    using var api = new QuadroApiClient();
    var response = await api.SendAsync(settings.Api, token.AccessToken!);

    ConsoleLogger.Info("API response body:");
    ConsoleLogger.Info(PrettyPrintJsonIfPossible(response.Body));

    if (!response.IsSuccessStatusCode)
    {
        ConsoleLogger.Warning("The API returned a non-success status code. See the response above for details.");
        Environment.ExitCode = 2;
    }
    else
    {
        ConsoleLogger.Success("Quadro API example completed successfully.");
    }
}
catch (OAuthTokenException ex)
{
    ConsoleLogger.Exception(ex, "OAuth token exchange failed.");

    if (!string.IsNullOrWhiteSpace(ex.ResponseBody))
    {
        ConsoleLogger.Error($"Token endpoint response body:{Environment.NewLine}{ex.ResponseBody}");
    }

    Environment.ExitCode = 3;
}
catch (Exception ex)
{
    ConsoleLogger.Exception(ex, "Unhandled exception.");
    Environment.ExitCode = 1;
}

static string PrettyPrintJsonIfPossible(string body)
{
    if (string.IsNullOrWhiteSpace(body))
    {
        return "<empty response>";
    }

    try
    {
        using var document = JsonDocument.Parse(body);
        return JsonSerializer.Serialize(
            document.RootElement,
            new JsonSerializerOptions { WriteIndented = true });
    }
    catch (JsonException)
    {
        return body;
    }
}
