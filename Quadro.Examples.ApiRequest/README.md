# Quadro API .NET voorbeeld — OAuth 2.0 Authorization Code + PKCE

Dit is een klein **.NET 8 Console**-project dat de huidige Postman-configuratie nabootst en daarna één configureerbare API-request uitvoert.

## Postman-instellingen die in dit voorbeeld zijn verwerkt

| Postman | .NET voorbeeld |
|---|---|
| OAuth 2.0 | Ja |
| Grant Type: Authorization Code With PKCE | Ja |
| Authorize using browser | Ja; de standaardbrowser wordt geopend |
| Auth URL | `OAuth.AuthorizationUrl` |
| Access Token URL | `OAuth.TokenUrl` |
| Client ID | `OAuth.ClientId` |
| Code Challenge Method: SHA-256 | `S256` |
| Scope | Niet meegestuurd |
| State | Niet meegestuurd |
| Client Authentication: Send client credentials in body | `client_id` staat in de token body; optioneel ook `client_secret` |
| Extra Auth Request: `audience` | `OAuth.Audience` |
| API method | `Api.Method`, standaard `GET` |

## Hoe “Authorize using browser” werkt

De applicatie:

1. genereert een cryptografisch willekeurige PKCE `code_verifier`;
2. maakt daarvan met SHA-256 een `code_challenge`;
3. start een lokale callback-listener;
4. opent de authorization URL in de standaardbrowser;
5. wacht tot de OAuth-server naar de lokale `RedirectUri` terugstuurt;
6. leest de `code` uit de callback;
7. wisselt deze code + `code_verifier` in bij de token URL;
8. gebruikt het ontvangen `access_token` als `Authorization: Bearer ...` voor de API-call.

**Belangrijk:** de gekozen `RedirectUri` moet ook als toegestane callback/redirect URI geregistreerd zijn bij de OAuth-client. Standaard gebruikt het voorbeeld:

```text
http://127.0.0.1:53682/callback
```

## Configureren

Vul `appsettings.json` in:

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

`ClientSecret` is optioneel. Je gaf vooralsnog alleen een Client ID op. Daarom verstuurt het voorbeeld altijd `client_id` in de token-request body en alleen een `client_secret` wanneer je die daadwerkelijk configureert.

### Via environment variables

Deze waarden kunnen de JSON-configuratie overschrijven:

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

Dat is met name handig voor credentials die je niet in een configuratiebestand wilt opslaan.

## Authorization request

De browser-request bevat conceptueel:

```text
?response_type=code
&client_id=...
&redirect_uri=...
&code_challenge=...
&code_challenge_method=S256
&audience=...
```

Er wordt bewust **geen `scope`** en **geen `state`** toegevoegd, conform de opgegeven Postman-configuratie.

`state` is normaal gesproken wel een aanbevolen OAuth-beveiligingsmaatregel tegen login-CSRF. Het is in deze versie weggelaten om eerst exact op de werkende Postman-flow aan te sluiten.

## Token request

De Access Token URL ontvangt een `application/x-www-form-urlencoded` POST met:

```text
grant_type=authorization_code
code=...
redirect_uri=...
client_id=...
code_verifier=...
```

Als `ClientSecret` is ingevuld wordt ook dit veld in dezelfde body toegevoegd:

```text
client_secret=...
```

Er wordt dus geen HTTP Basic Authentication gebruikt voor de client credentials.

## API request

Standaard:

```json
"Method": "GET"
```

Maar de code gebruikt `HttpMethod` dynamisch. Je kunt bijvoorbeeld instellen:

```json
"Method": "POST",
"JsonBody": "{\"example\":true}"
```

Ook `PUT`, `PATCH`, `DELETE`, `HEAD` of een andere geldige HTTP-methode kan worden ingevuld.

Extra headers kunnen in `Headers`:

```json
"Headers": {
  "X-Custom-Header": "waarde"
}
```

De access token wordt automatisch toegevoegd als:

```text
Authorization: Bearer <access_token>
```

## Starten

```powershell
copy appsettings.example.json appsettings.json
# vul appsettings.json in
dotnet run
```

Bij een succesvolle login zie je daarna de HTTP-status en de response body van de ingestelde API-call. JSON wordt voor leesbaarheid ingesprongen.

## Projectbestanden

- `Program.cs` — login uitvoeren en daarna de API-call doen
- `ConsoleLogger.cs` — logging met timestamps en vaste consolekleuren per logniveau
- `PkceOAuthClient.cs` — browser, PKCE, callback en token exchange
- `QuadroApiClient.cs` — configureerbare HTTP-request met Bearer token
- `AppSettings.cs` — configuratie en environment overrides
- `appsettings.example.json` — voorbeeldconfiguratie
- `appsettings.json` — lokale configuratie; staat in `.gitignore`

## Opmerking over bouwen

De projectbestanden zijn voor .NET 8 geschreven. In de uitvoeromgeving waarin dit voorbeeld is samengesteld is geen .NET SDK aanwezig, dus hier kon geen daadwerkelijke `dotnet build` worden uitgevoerd.

## Logging

De console-uitvoer gebruikt vaste kleuren per niveau:

- **Wit** — informatie (`INFO`)
- **Groen** — succesvolle stappen (`OK`)
- **Geel** — waarschuwingen en niet-succesvolle HTTP-responses (`WARN`)
- **Rood** — errors en exceptions (`ERROR` / `EXCEPTION`)

Elke regel bevat een timestamp en logniveau. Gevoelige waarden zoals access tokens, authorization codes, PKCE verifiers en client secrets worden bewust niet gelogd.

Alle comments in de C#-broncode zijn Engelstalig.
