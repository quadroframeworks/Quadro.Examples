# Quadro API Example (.NET 8)

Dit project is een klein .NET 8 consolevoorbeeld voor het benaderen van de Quadro API met **OAuth 2.0 Authorization Code + PKCE (SHA-256)**.

De applicatie opent de standaardbrowser voor de login, ontvangt de OAuth-callback lokaal, haalt een access token op en voert daarna de geconfigureerde API-request uit.

## Snel starten

### 1. Vereisten

Zorg dat het volgende is geïnstalleerd:

- .NET 8 SDK
- een browser
- toegang tot de juiste OAuth- en API-instellingen

Controleer eventueel je .NET-installatie met:

```powershell
dotnet --version
```

### 2. Maak `appsettings.json`

Het project bevat bewust **geen kant-en-klare `appsettings.json`**.

Gebruik `appsettings.example.json` als uitgangspunt en hernoem of kopieer dit bestand naar:

```text
appsettings.json
```

In Windows Verkenner of Visual Studio kun je het bestand simpelweg kopiëren en de kopie hernoemen.

Via PowerShell kan dat bijvoorbeeld zo:

```powershell
Copy-Item appsettings.example.json appsettings.json
```

Of via Command Prompt:

```cmd
copy appsettings.example.json appsettings.json
```

> `appsettings.json` staat in `.gitignore`. Dit is bewust gedaan zodat lokale instellingen en eventuele credentials niet per ongeluk in Git terechtkomen.

### 3. Vul de instellingen in

Open daarna `appsettings.json` en vul de waarden in die bij jouw omgeving horen:

```json
{
  "OAuth": {
    "AuthorizationUrl": "https://jouw-auth-url",
    "TokenUrl": "https://jouw-access-token-url",
    "ClientId": "jouw-client-id",
    "ClientSecret": "",
    "Audience": "jouw-audience",
    "RedirectUri": "http://127.0.0.1:53682/callback",
    "BrowserTimeoutSeconds": 300
  },
  "Api": {
    "RequestUrl": "https://jouw-api-url/jouw-endpoint",
    "Method": "GET",
    "JsonBody": null,
    "Headers": {}
  }
}
```

De belangrijkste instellingen zijn:

| Instelling | Betekenis |
|---|---|
| `OAuth.AuthorizationUrl` | OAuth authorization endpoint |
| `OAuth.TokenUrl` | Endpoint waar de authorization code wordt ingewisseld voor een token |
| `OAuth.ClientId` | Client ID van de OAuth-client |
| `OAuth.ClientSecret` | Optioneel client secret; leeg laten wanneer niet nodig |
| `OAuth.Audience` | Waarde voor de extra `audience` parameter in de authorization request |
| `OAuth.RedirectUri` | Lokale callback-URL voor de browser-login |
| `Api.RequestUrl` | Volledige URL van de API-call |
| `Api.Method` | HTTP-methode, bijvoorbeeld `GET`, `POST`, `PUT`, `PATCH` of `DELETE` |
| `Api.JsonBody` | Optionele JSON-body voor requests zoals POST of PUT |
| `Api.Headers` | Optionele extra HTTP-headers |

## Redirect URI

Standaard gebruikt het project:

```text
http://127.0.0.1:53682/callback
```

Deze URL moet als toegestane **Redirect URI / Callback URL** geregistreerd zijn bij de OAuth-client. Als dat niet zo is, kan de authorization server de browser-login weigeren.

Je kunt de URL en poort aanpassen via `OAuth.RedirectUri`, maar zorg er dan voor dat exact dezelfde waarde ook bij de OAuth-client is toegestaan.

## Applicatie starten

Ga in een terminal naar de projectmap en voer uit:

```powershell
dotnet run
```

De applicatie doet daarna automatisch het volgende:

1. Leest en valideert `appsettings.json`.
2. Genereert een PKCE `code_verifier`.
3. Maakt met SHA-256 de bijbehorende `code_challenge`.
4. Start een lokale callback-listener.
5. Opent de OAuth Authorization URL in de standaardbrowser.
6. Laat je inloggen en toestemming geven via de browser.
7. Ontvangt de authorization code via de lokale callback.
8. Wisselt de code in voor een access token.
9. Voert de ingestelde API-request uit met `Authorization: Bearer <token>`.
10. Toont de HTTP-status en response in de console.

## OAuth-instellingen

Dit voorbeeld volgt de gebruikte Postman-configuratie:

- OAuth 2.0
- Grant type: **Authorization Code With PKCE**
- Browser authorization: **aan**
- PKCE code challenge method: **SHA-256 / S256**
- Geen scope
- Geen state
- Client credentials worden bij de token request in de body verstuurd
- Extra authorization parameter: `audience`

De authorization request bevat onder andere:

```text
response_type=code
client_id=...
redirect_uri=...
code_challenge=...
code_challenge_method=S256
audience=...
```

De token request wordt als `application/x-www-form-urlencoded` verzonden en bevat onder andere:

```text
grant_type=authorization_code
code=...
redirect_uri=...
client_id=...
code_verifier=...
```

Wanneer `OAuth.ClientSecret` is ingevuld, wordt ook `client_secret` in de body meegestuurd.

## API-request aanpassen

Voor een GET-request:

```json
"Api": {
  "RequestUrl": "https://jouw-api-url/jouw-endpoint",
  "Method": "GET",
  "JsonBody": null,
  "Headers": {}
}
```

Voor bijvoorbeeld een POST-request:

```json
"Api": {
  "RequestUrl": "https://jouw-api-url/jouw-endpoint",
  "Method": "POST",
  "JsonBody": "{\"name\":\"Example\"}",
  "Headers": {}
}
```

Extra headers kunnen als volgt worden toegevoegd:

```json
"Headers": {
  "X-Custom-Header": "example-value"
}
```

Het Bearer access token hoef je niet zelf als header toe te voegen. Dat doet de applicatie automatisch.

## Logging

De applicatie logt de belangrijkste stappen in de console en gebruikt kleuren om de uitvoer snel leesbaar te maken:

- **Wit — INFO:** normale informatie en voortgang
- **Groen — OK:** succesvolle acties
- **Geel — WARN:** waarschuwingen en onverwachte/niet-succesvolle HTTP-responses
- **Rood — ERROR / EXCEPTION:** fouten en exceptions

Elke logregel bevat een timestamp en logniveau.

Gevoelige OAuth-waarden worden bewust niet in de logs geschreven. Dit geldt onder andere voor:

- access tokens
- authorization codes
- PKCE code verifiers
- client secrets

## Environment variables

De belangrijkste instellingen uit `appsettings.json` kunnen ook via environment variables worden overschreven:

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

Dit is vooral handig wanneer je gevoelige waarden niet in `appsettings.json` wilt bewaren.

## Projectstructuur

- `Program.cs` — start de OAuth-flow en voert daarna de API-call uit
- `PkceOAuthClient.cs` — PKCE, browser-login, callback en token exchange
- `QuadroApiClient.cs` — bouwt en verstuurt de API-request
- `AppSettings.cs` — leest en valideert de configuratie
- `ConsoleLogger.cs` — centrale consolelogging met kleurcodering
- `appsettings.example.json` — voorbeeldconfiguratie; eerst kopiëren/hernoemen
- `.gitignore` — voorkomt onder andere dat `appsettings.json` wordt gecommit

## Veelvoorkomende problemen

### `Configuration file 'appsettings.json' was not found`

`appsettings.example.json` is nog niet gekopieerd of hernoemd.

Maak in de projectmap een bestand met exact deze naam:

```text
appsettings.json
```

### De browser meldt dat de redirect URI niet geldig is

Controleer of de waarde van `OAuth.RedirectUri` exact overeenkomt met een toegestane redirect URI van de OAuth-client.

### De browser opent niet

Controleer of er een standaardbrowser is ingesteld. De authorization URL wordt ook in de console gelogd, zodat je deze indien nodig handmatig kunt openen.

### HTTP 401 of 403 bij de API-call

Controleer onder andere:

- `ClientId`
- `Audience`
- de gebruikte OAuth-client
- de API URL
- of de ingelogde gebruiker toegang heeft tot het gevraagde endpoint

### Andere HTTP-fout

De applicatie toont de HTTP-status en, indien beschikbaar, de response body. Die informatie is meestal het beste uitgangspunt voor verdere diagnose.

## Security

Commit `appsettings.json` niet wanneer daar omgevingsspecifieke of gevoelige waarden in staan. Het bestand staat daarom standaard in `.gitignore`.

Voor lokale ontwikkeling kun je gevoelige waarden eventueel via environment variables instellen in plaats van ze rechtstreeks in het configuratiebestand te plaatsen.
