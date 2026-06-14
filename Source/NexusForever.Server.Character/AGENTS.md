# Microservices

Covers distributed server microservices: Character, Chat, Friendship, and Group servers. Each handles a specific domain of player state management via independent .NET HostBuilder processes coordinated by Aspire.

## Purpose

Define rules for the four Server.* microservices that run as independent processes in .NET Aspire. They handle persistent player data and social features separate from the game world simulation, communicating over Rebus (RabbitMQ/Azure Service Bus) and REST API clients. Each service has its own database context, background jobs, and network message handlers for inter-service communication.

## Ownership

- **Server.Character**: Character persistence bridge between WorldServer and Database.Query. Manages character entity caching (`CharacterManager`), Who system (in-memory rate-limited query engine with `PartitionedRateLimiter<Identity>`). DI chain: `AddQueryDatabase → AddCharacterAPIClient → PartitionedRateLimiter for Who → AddNetworkInternalBroker + handlers → AddGame()`. Subscribes to Player(2) + Who(1) messages.
- **Server.ChatServer**: Chat channel management, message relay via outbox pattern with Quartz-scheduled `OutboxScheduledJob` (polls every 1 second). Handles channel join/leave/kick/moderate/mute/password/text-request (~10 handlers), plus Group/Guild/Player/Server sync messages. Total ~24 handler files implementing Chat(7+30 formats) + Group(5) + Guild(6) + Server(2) + Player(4).
- **Server.Friendship**: Relationship management with three-layer model: Account friendship (bidirectional via `AccountFriendInverse`), Character friendship (per-character lists), and Public friend discovery (`Friend`/`FriendAccount` shared identity layer). Factories (`FriendFactory`, `FriendAccountFactory`) + invite validators enforce state machine: Pending → Accepted/Rejected/Expired. Rate limiting via `LimitOptions`, invite cooldowns via `InviteOptions`. Subscribes to Friend(18+) + Player(5) messages.
- **Server.GroupServer**: Party/raid group lifecycle (`Group` → `GroupInvite` → `GroupRequest` → `GroupMember`), leader election, match coordination with instanced content (MatchCreated/MemberLeft/Removed). Five Quartz jobs: `CharacterDirtyRealmStats`, `CharacterDirtyRealm`, `GroupDirtyPosition`, `GroupInviteExpired`, `GroupRequestExpired`. Subscribes to Group(13) + Match(3) + Player(9) messages. Total ~26 handler files.

## Local Contracts

- **Shared pattern across all four services**: `*Server.cs` entry point → `HostedService.cs` (Rebus subscription/coordination) → `Network/Internal/Handler/` (message handlers) → optional Quartz jobs for periodic persistence
- **Identity system**: Each service has its own shared files (`Identity.cs`, `IdentityMappingExtensions.cs`, `IdentityName.cs`) — these are NOT cross-service types, each service defines Identity independently to avoid circular dependencies
- **Configuration**: `*Server.example.json` + `Configuration/` directory. Database connection strings via `Database:<Name>` sections in app settings. API clients configured via `APIConfig` for Character/Account REST endpoints
- **NLog**: Trace-level logging enabled by default across all services, Windows Service + Systemd support via .NET Generic Host infrastructure

## Work Guidance

### Inter-Service Communication Flow

All microservices communicate via Rebus over RabbitMQ or Azure Service Bus. Each service subscribes to a specific message catalog — do not add handlers for messages outside the subscribed set without coordinating with the owning service:

| Subscriber | Messages | Purpose |
|---|---|---|
| Character | Player(2), Who(1) | Sync character login/logout, handle who-queries |
| Chat | Chat(7+30 formats), Group(5), Guild(6), Server(2), Player(4) | Channel management, message relay, guild sync, world state |
| Friendship | Friend(18+), Player(5) | Account/character friend invites, presence tracking, online/offline sync |
| Group | Group(13), Match(3), Player(9) | Party/raid coordination, match lifecycle, position/stat updates from WorldServer |

**Publishing messages**: Use `IInternalMessagePublisher` via the outbox pattern — never publish directly from handler code. The publisher writes transactionally to the service's database (`InternalMessageModel` table with `type=AssemblyQualifiedName`, `payload=JSON`). A scheduled Quartz job polls every second, deserializes JSON, and publishes through Rebus bus. For urgent messages (e.g., channel text), use `PublishUrgentAsync()` which adds to `_urgentMessages` list for immediate processing via `OutboxUrgentSignal.WriteUrgent() + FlushUrgentMessages()`.

### CharacterManager Cache-and-Fetch Pattern

The `CharacterManager` in Server.Character implements a cache-first bridge between WorldServer and character persistence:
1. Query Database.Query first (fast in-memory cache or read-optimized query)
2. If not found, call API.Character.Client REST endpoint to fetch full character data
3. Convert result to `CharacterModel`, save back to Database.Character via Repository
4. This ensures WorldServer can always resolve character state even when the Character database is temporarily unreachable

**Never bypass this pattern** — direct database queries from other services will miss data that only exists in API.Character.Client responses (e.g., dynamically computed stats).

### Friendship Invite State Machine

All friendship operations flow through a strict state machine:
1. `Pending` → invite created, awaiting response
2. On accept → transition to `Accepted`, create both `AccountFriend` + `AccountFriendInverse` records for bidirectional tracking
3. On reject/timeout → transition to `Expired` or `Rejected`, cleanup invite records
4. Rate limits enforced via `LimitOptions.PermitLimit` and `InviteOptions.CooldownSeconds`

**Key rule**: Character friend lists are separate from account-level friends — a player may have different character-specific friends that don't persist across accounts on the same account.

### Group State Machine & Match Coordination

Group lifecycle follows: empty → invite accepted → active (with members) → disbanded/destroyed. During instanced content loading, groups transition into match states via `MatchCreated` message from Network.Internal. The GroupServer coordinates:
- Leader election when current leader disconnects (first valid member in roster order)
- Match state transitions: `MatchEntered`, `MatchExited`, `PvpMatchState`, `PvpMatchFinish`, `PvpMatchStart`
- Position/stat updates synced from WorldServer via Player messages — GroupServer maintains authoritative group position data

**Critical**: Group state changes must be atomic. If a member join/leave fails midway, rollback all partial state (invites, markers, loot rules). Use explicit transaction scope around multi-member operations.

### Thread Safety & Concurrency Model

- All handler methods run on Rebus worker threads — multiple handlers may process messages concurrently for the same entity identity. Use concurrent collections (`ConcurrentDictionary`, `ConcurrentBag`) or explicit locks for shared in-memory caches (e.g., CharacterManager's cache, ChatChannel subscription maps).
- Quartz jobs run on separate scheduled threads — protect shared state with lock statements when modifying data that handlers also read/write.
- The outbox pattern inherently provides concurrency safety: handlers write to database transactionally, the scheduled job reads asynchronously — no in-memory race conditions between publishing and processing.

### Configuration & Deployment Patterns

Each service's config file (`*Server.json`) must include:
```json
{
  "Database": { "<ServiceName>": { "ConnectionString": "...", "Provider": "MySql" } },
  "Api": { "Account": { "BaseUrl": "http://localhost:<port>" }, "Character": { "BaseUrl": "http://localhost:<port>" } },
  "Broker": { "Broker": "RabbitMq", "ConnectionString": "...", "InputQueue": "<ServiceName>.input" }
}
```

- `Database.<ServiceName>` connection string maps to the correct Database.* context project via `[Database(DatabaseType.X)]` attribute resolution in the DatabaseManager pattern.
- API client URLs must match running Aspire service endpoints — use named services from Aspire (e.g., `http://nexusforever-api-account`) when deployed, localhost for local dev.

## Verification

- `dotnet build Source/NexusForever.slnx` — compiles all microservices with their dependencies
- Manual testing: start Aspire host (`NexusForever.Aspire.AppHost`), verify each service connects to RabbitMQ input queues and processes messages from its subscribed catalog
- Outbox pattern verification: trigger a chat message, check Database.Chat's `InternalMessageModel` table for the queued entry, confirm Rebus delivers it within 1 second (poll interval)
- Cross-service integration test flow: create character via API.Character → CharacterManager cache-and-fetch resolves it → join group via GroupServer → send chat → verify friendship invite state machine transitions correctly

## Child DOX Index

None. All four server services share the same structure and complexity level.
