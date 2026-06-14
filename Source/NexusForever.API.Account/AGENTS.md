# API Domain

Covers REST API endpoints, shared API models, and HTTP client proxies for inter-service communication.

## Purpose

Define rules for `NexusForever.API` (shared models), `NexusForever.API.Account` (account REST service), `NexusForever.API.Character` (character REST service), and their corresponding client proxy projects (`API.Account.Client`, `API.Character.Client`).

## Ownership

- **API**: Shared model types used across all API consumers — account/character DTOs, error responses
- **API.Account**: Account REST endpoints for registration, profile management, authentication delegation
- **API.Character**: Character REST endpoints for character data, progression updates, inventory queries
- **API.Account.Client**: HTTP client proxy that calls API.Account; used by other services for account lookups
- **API.Character.Client**: HTTP client proxy that calls API.Character; used by WorldServer and game logic

## Architecture

### Project Structure

5 projects, ~16 source files total (excluding obj/):

**NexusForever.API (shared models)** — `Model/` directory:
| File | Type | Purpose |
|---|---|---|
| `Account.cs`, `Character.cs`, `CharacterStat.cs` | DTO | Data transfer objects for client responses |
| `IdentityName.cs` | Value object | Shared identity by name (e.g., character lookup) |
| `Position.cs` | Value object | X/Y/Z coordinates + rotation |
| `ProblemDetails.cs` | Error model | RFC 7807-compatible error response structure |
| `Identity.cs` | Value object | GUID-based identity reference |

**NexusForever.API.Account (entry point)** — `AccountAPI.cs`:
```
Main() → WebApplication.CreateBuilder() → AddAuthDatabase() → Scoped<AccountManager>() → MapGetAccountEndpoint() → Run()
```
Minimal ASP.NET Core web host with NLog logging, Windows service/SystemD support (`UseWindowsService()`/`UseSystemd()`), config from `AccountAPI.json` + environment variables.

**NexusForever.API.Character (entry point)** — `CharacterAPI.cs`:
```
Main() → AddAuthDatabase() → AddDbContextKeyed<CharacterContext>(realmId) → ServerManager + CharacterManager → MapGetCharacterEndpoint()
```
Multi-realm keyed DbContext registration: each realm's database is registered with its `RealmId` key via config section `Database:Character` (list of `DatabaseConnectionStringWithRealm`).

### Dependency Injection Patterns

**AccountAPI**: Simple scoped registration — `AccountRepository` (from Database.Auth) → `Scoped<AccountManager>`. Single database dependency.

**CharacterAPI**: Multi-realm keyed DbContext pattern — each realm's database registered via `AddDbContextKeyed<CharacterContext>(realmId, options => ...)`, `ServerManager` tracks live character server registrations, `CharacterManager` bridges between them:
```csharp
// CharacterManager resolves at runtime
Server server = await _serverManager.GetServerAsync(realmId);  // find which server hosts this realm
CharacterContext context = _contextFactory.GetContext(server.Id);  // keyed DbContext for that realm's DB
var repository = new CharacterRepository(context);  // fresh per request (no DI lifetime management)
```

### Client Proxy Architecture

**APIClient base class** (`NexusForever.API/APIClient.cs`):
- Constructor-injected `HttpClient` (typed HTTP client pattern via `ServiceCollectionExtensions`)
- Generic `Get<T>(url, CancellationToken)` method handles:
  - Success (200) → `ReadFromJsonAsync<T>()` deserialization
  - Not Found (404) → returns `default(T)` (no exception for missing resources)
  - Error responses with `application/problem+json` content → deserializes `ProblemDetails`, throws `HttpRequestException` with title/detail message
  - Other errors → generic `HttpRequestException`

**AccountAPIClient**: Used by other services (microservices, WorldServer) for account lookups — resolves account ID or email to `Model.Account.Account`.

**CharacterAPIClient**: Used by WorldServer during login/teleport/validation flows — resolves character by realm+ID or realm+name.

### Mapping Convention

All entity-to-DTO conversion uses extension methods in `*ManagerMappingExtensions.cs`:
- `AccountManager.ToAccount()` — database entity → API model (no server context needed)
- `CharacterManager.ToCharacter(Server)` — includes server metadata (realm ID, IP) during mapping

Both endpoint registration and DTO mapping follow the convention of static method extension patterns for discoverability.

## Local Contracts

- Each API server follows `*API.cs` + `Configuration/` + `Endpoint/` structure
- Config files: `*API.example.json` with port, CORS, and database connection settings
- Endpoints are organized under `Endpoint/` directories by resource (Account/, Character/)
- Client proxies use typed HTTP clients with retry logic for resilience

## Work Guidance

### Creating a New Endpoint
1. Add DTO to `NexusForever.API/Model/` (e.g., `NewResource.cs`)
2. Create endpoint file in `*API/Endpoint/GetNewResourceEndpoint.cs` using Minimal API convention (`MapGet*Endpoint()`)
3. Use manager class for data access — never query DbContext directly from endpoints
4. Register via extension method on the main API entry point

### Multi-Realm Character Queries (CharacterManager Gotcha)
- `ServerManager` must be queried first to find which server hosts a realm before accessing character data
- `_contextFactory.GetContext(server.Id)` returns keyed DbContext for that specific realm's database — pass `server.Id`, not `realmId`
- `Server` object is passed to `ToCharacter(Server)` mapping extension for enrichment with server metadata

### APIClient Error Handling (Gotcha)
- 404 responses return `default(T)` silently — callers must always check null/IsDefault before using results
- Only errors with `application/problem+json` content include descriptive messages in exceptions
- Always pass cancellation tokens through the call chain for proper request timeout handling

### Shared Model Versioning Rules
- Add new fields to DTOs only — never remove or reorder existing ones (backward compatibility)
- New endpoints returning different DTO shapes must create separate response models, not modify shared types
- `CharacterStat` is a per-character stat entry; extensions that add stats should use additional model properties

### API Responses and Authentication
- API responses must include proper HTTP status codes — never return 200 for error conditions
- Authentication: API.Account delegates to AuthServer; never store raw credentials in the API layer
- Rate limiting should be considered for all public-facing endpoints
- Client proxies must handle service unavailability gracefully (circuit breaker pattern recommended)

## Verification

- `dotnet build Source/NexusForever.slnx` — compiles API layer with all dependencies
- Manual testing: run API servers alongside WorldServer and verify inter-service calls via client proxies
- Test endpoint responses match expected DTO schemas from shared API models

## Child DOX Index

None. Account and Character APIs share the same structure; client proxies follow identical patterns.
