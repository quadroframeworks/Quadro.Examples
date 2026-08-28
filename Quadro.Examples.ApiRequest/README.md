# Quadro API Example (.NET 8)

**Language:** **English** | [Nederlands](README.nl.md)

This project is a small .NET 8 console example for accessing the Quadro API using **OAuth 2.0 Authorization Code + PKCE (SHA-256)**.

The application opens the default browser for authentication, receives the OAuth callback locally, obtains an access token, and then performs the configured API request.

## Quick start

### 1. Requirements

Make sure the following are installed or available:

- .NET 8 SDK
- a web browser
- access to the required OAuth and API settings

You can verify your .NET installation with:

```powershell
dotnet --version
```

### 2. Create `appsettings.json`

The project intentionally does **not** include a ready-to-use `appsettings.json` file.

Use `appsettings.example.json` as a template and copy or rename it to:

```text
appsettings.json
```

Using PowerShell:

```powershell
Copy-Item appsettings.example.json appsettings.json
```

Or using Command Prompt:

```cmd
copy appsettings.example.json appsettings.json
```

> `appsettings.json` is listed in `.gitignore`. This is intentional, so local configuration and possible credentials are not accidentally committed to Git.

### 3. Configure the application

Open `appsettings.json` and enter the values for your environment:

```json
{
  "OAuth": {
    "AuthorizationUrl": "https://your-authorization-url",
    "TokenUrl": "https://your-access-token-url",
    "ClientId": "your-client-id",
    "ClientSecret": "",
    "Audience": "your-audience",
    "RedirectUri": "http://127.0.0.1:53682/callback",
    "BrowserTimeoutSeconds": 300
  },
  "Api": {
    "RequestUrl": "https://your-api-url/your-endpoint",
    "Method": "GET",
    "JsonBody": null,
    "Headers": {}
  }
}
```

The main settings are:

| Setting | Description |
|---|---|
| `OAuth.AuthorizationUrl` | OAuth authorization endpoint |
| `OAuth.TokenUrl` | Endpoint used to exchange the authorization code for an access token |
| `OAuth.ClientId` | Client ID of the OAuth client |
| `OAuth.ClientSecret` | Optional client secret; leave empty when it is not required |
| `OAuth.Audience` | Value for the additional `audience` parameter in the authorization request |
| `OAuth.RedirectUri` | Local callback URL used during browser authentication |
| `Api.RequestUrl` | Full URL of the API request |
| `Api.Method` | HTTP method, such as `GET`, `POST`, `PUT`, `PATCH`, or `DELETE` |
| `Api.JsonBody` | Optional JSON body for requests such as POST or PUT |
| `Api.Headers` | Optional additional HTTP headers |

## Redirect URI

By default, the project uses:

```text
http://127.0.0.1:53682/callback
```

This URL must be registered as an allowed **Redirect URI / Callback URL** for the OAuth client. Otherwise, the authorization server may reject the browser login.

You can change the URL and port through `OAuth.RedirectUri`, but the exact same value must also be allowed by the OAuth client configuration.

## Run the application

Open a terminal in the project directory and run:

```powershell
dotnet run
```

The application then automatically performs the following steps:

1. Reads and validates `appsettings.json`.
2. Generates a PKCE `code_verifier`.
3. Creates the corresponding SHA-256 `code_challenge`.
4. Starts a local callback listener.
5. Opens the OAuth Authorization URL in the default browser.
6. Lets the user authenticate and authorize through the browser.
7. Receives the authorization code through the local callback.
8. Exchanges the code for an access token.
9. Sends the configured API request with `Authorization: Bearer <token>`.
10. Displays the HTTP status and response in the console.

## OAuth configuration

This example follows the Postman configuration used for the API:

- OAuth 2.0
- Grant type: **Authorization Code With PKCE**
- Browser authorization: **enabled**
- PKCE code challenge method: **SHA-256 / S256**
- No scope
- No state
- Client credentials are sent in the token request body
- Additional authorization parameter: `audience`

The authorization request contains, among other values:

```text
response_type=code
client_id=...
redirect_uri=...
code_challenge=...
code_challenge_method=S256
audience=...
```

The token request is sent as `application/x-www-form-urlencoded` and contains, among other values:

```text
grant_type=authorization_code
code=...
redirect_uri=...
client_id=...
code_verifier=...
```

If `OAuth.ClientSecret` is configured, `client_secret` is also included in the request body.

## Configure the API request

For a GET request:

```json
"Api": {
  "RequestUrl": "https://your-api-url/your-endpoint",
  "Method": "GET",
  "JsonBody": null,
  "Headers": {}
}
```

For example, for a POST request:

```json
"Api": {
  "RequestUrl": "https://your-api-url/your-endpoint",
  "Method": "POST",
  "JsonBody": "{\"name\":\"Example\"}",
  "Headers": {}
}
```

Additional headers can be configured as follows:

```json
"Headers": {
  "X-Custom-Header": "example-value"
}
```

You do not need to add the Bearer access token manually. The application adds the `Authorization` header automatically.

## Logging

The application logs the important steps to the console and uses colors to make the output easier to scan:

- **White — INFO:** normal information and progress
- **Green — OK:** successful operations
- **Yellow — WARN:** warnings and unexpected or unsuccessful HTTP responses
- **Red — ERROR / EXCEPTION:** errors and exceptions

Each log entry contains a timestamp and log level.

Sensitive OAuth values are deliberately excluded from the logs, including:

- access tokens
- authorization codes
- PKCE code verifiers
- client secrets

## Environment variables

The main settings in `appsettings.json` can also be overridden through environment variables:

```text
QUADRO_OAUTH_AUTH_URL
QUADRO_OAUTH_TOKEN_URL
QUADRO_OAUTH_CLIENT_ID
QUADRO_OAUTH_CLIENT_SECRET
QUADRO_OAUTH_AUDIENCE
QUADRO_OAUTH_REDIRECT_URI
QUADRO_API_URL
QUADRO_API_METHOD
QUADRO_API_BODY
```

This is especially useful when you do not want to store sensitive values directly in `appsettings.json`.

## Project structure

- `Program.cs` — starts the OAuth flow and then performs the API request
- `PkceOAuthClient.cs` — handles PKCE, browser authentication, callback handling, and token exchange
- `QuadroApiClient.cs` — builds and sends the API request
- `AppSettings.cs` — reads and validates the configuration
- `ConsoleLogger.cs` — centralized color-coded console logging
- `appsettings.example.json` — example configuration; copy or rename this file first
- `.gitignore` — prevents files such as `appsettings.json` from being committed

## Troubleshooting

### `Configuration file 'appsettings.json' was not found`

`appsettings.example.json` has not been copied or renamed yet.

Create a file in the project directory with exactly this name:

```text
appsettings.json
```

### The browser reports an invalid redirect URI

Check that the value of `OAuth.RedirectUri` exactly matches one of the allowed redirect URIs configured for the OAuth client.

### The browser does not open

Make sure a default browser is configured. The authorization URL is also written to the console, so you can open it manually if necessary.

### HTTP 401 or 403 from the API

Check the following values and permissions:

- `ClientId`
- `Audience`
- the OAuth client being used
- the API URL
- whether the authenticated user has access to the requested endpoint

### Other HTTP errors

The application displays the HTTP status and, when available, the response body. This information is usually the best starting point for further troubleshooting.

## Security

Do not commit `appsettings.json` when it contains environment-specific or sensitive values. The file is therefore included in `.gitignore` by default.

For local development, sensitive values can also be supplied through environment variables instead of storing them directly in the configuration file.
