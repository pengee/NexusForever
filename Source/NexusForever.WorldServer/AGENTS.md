# World Server

Covers the primary game server handling in-world entity simulation, player state, zone management, and all client-facing infrastructure.

## Purpose

Define rules for `NexusForever.WorldServer`, the largest executable project. It hosts the main world tick loop, manages entities (players, NPCs) across zones/instances, loads and executes zone scripts at runtime, serves an embedded admin web console, and coordinates inter-service messaging via Rebus. The server runs as a .NET Aspire-hosted application with three background services: `HostedService` (main game loop), `NetworkInternalHandlerHostedService` (Rebus message subscriptions), and `OnlineHostedService` (online/offline broadcasts).

## Ownership

- WorldServer owns all in-world simulation logic including entity AI, player movement, combat resolution
- Message handlers for World protocol live under `Network/Message/Handler/`, organized by domain subdirectory
- Commands expose admin/debug functionality via the attribute-driven command framework (`CommandManager`)
- Zone scripts (from Script projects) are loaded dynamically at startup and ticked each world update
- Admin web console provides REST API endpoints and WebSocket command interface for server administration

## Initialization Order

Systems initialize in strict dependency order — altering this sequence breaks cross-references:

1. **Config**: `SharedConfiguration` → `DatabaseConfig` → DB migration via `DatabaseManager.Migrate()`
2. **Core Managers**: `RBACManager` (must precede CommandManager) → `DisableManager`
3. **Script Loading**: `ScriptManager.Initialise()` — loads assemblies from configured directory + dynamic source compilation
4. **Game Tables**: `GameTableManager.Initialise()` — loads all `.tbl` files via GameTable project
5. **Map Systems**: `MapIOManager` (load zone maps) → `SearchManager` (entity indexing) → `EntityManager` → `EntityCommandManager` (movement command factories) → `EntityCacheManager` → `FactionManager`
6. **Game Subsystems** (order matters for cross-references):
   - `GlobalCinematicManager`
   - `ChatFormatManager`
   - `GlobalAchievementManager` — must initialise before guilds
   - `GlobalGuildManager` — must initialise before residences
   - `CharacterManager` — must initialise before residences
   - `GlobalResidenceManager`
7. **Assets & Managers**: `AssetManager` → `ItemManager` → `GlobalSpellManager` → `GlobalQuestManager` → `GlobalStorefrontManager` → `ServerManager` (realm ID setup) → `TextFilterManager` → `CustomisationManager`
8. **Matching**: `matchingManager.Initialise()`
9. **Message Registration**: `messageManager.RegisterNetworkManagerMessagesAndHandlers()` + `RegisterNetworkManagerWorldMessages()` + `RegisterNetworkManagerWorldHandlers()`
10. **World Manager** (last init): Creates the world tick loop with update ordering — Network → Map → Buyback → Quest → Guild → Residence → LoginQueue → Matching/Match → Script → Shutdown → Commands (always last)
11. **Network Start**: `networkManager.Initialise()` + `Start()`
12. **Command Manager** (`CommandManager.Initialise()`)

### Shutdown Sequence

Shutdown reverses initialization in a specific order to ensure data integrity:
1. Network manager stop accepting connections
2. Command thread join (blocks until console input loop exits)
3. ServerManager ping notifications to other services
4. World loop shutdown
5. Residence/Guild persistence save
6. All player sessions saved via `SaveDirect()` on each session

## Hosted Services

### HostedService (Main Game Loop)
The primary service that wires all subsystems together and runs the world tick. Responsible for:
- Initializing every game system in dependency order
- Setting up the world update callback chain (`lastTick` delegate)
- Starting/stopping the network manager, command thread, and all managers

### NetworkInternalHandlerHostedService (Inter-Service Messaging)
Subscribes to 50+ Rebus bus message types for handling inter-service communication. Handles messages from ChatServer (7 types), Friendship service (19 types), GroupServer (18 types), plus Player info requests (2) and Who queries (1). Subscriptions occur during `StartAsync()` — if a handler is missing, the message will be silently dropped on the Rebus bus side.

### OnlineHostedService
Publishes `ServerWorldOnlineMessage` on startup with RealmId, and `ServerWorldOfflineMessage` on shutdown. This notifies other services (AuthServer, etc.) of this world's availability state.

## Local Contracts

- **Entry point**: `WorldServer.cs`; uses .NET Generic Host pattern (`HostBuilder`)
- **Configuration**: Reads from `WorldServer.json` / `WorldServer.example.json`. Key sections:
  - `Network`: Port (default 24000), socket settings, internal broker config
  - `Realm`: Realm ID, max player count (enforced by LoginQueueManager)
  - `Script`: Enable flag, assembly directory path, dynamic compilation settings (`Dynamic.Enable` for hot-reload)
  - `GameTable`: Path to game table `.tbl` files
- **Message handlers**: Implement `IMessageHandler<ISession, TMessage>` — auto-discovered via reflection at startup by scanning all assemblies for types implementing the generic interface. Registration happens in `Network/MessageManagerExtensions.RegisterNetworkManagerWorldHandlers()`
- **Commands**: Use `[Command]` attribute on handler classes; parameter converters use `IParameterConvert` with `[Convert(Type)]` attributes

## Login Queue Manager

`LoginQueueManager` enforces realm population limits and manages the connection queue:

- **Max players**: Set via `RealmConfig.MaxPlayers`; checked every 5 seconds during update tick
- **GM bypass**: Accounts with `Permission.GMFlag` are exempt from queue — admitted immediately
- **Queue rejoin**: If a queued player disconnects and reconnects, they preserve their position in the queue (checked via session ID lookup)
- **Wait time formula**: Estimated wait = `queuePosition × 30 seconds` (sent to client via `ServerQueueStatus`)
- **Admission flow**: On admission, sends `ServerQueueFinish` packet then triggers `characterListManager.SendCharacterListPackets(session)`

## Script Loading

Scripts support dual loading modes for development flexibility:

1. **Assembly mode** (production): Pre-compiled DLLs loaded from the directory specified by `ScriptConfig.Directory`. Assembly metadata is discovered via reflection at startup
2. **Dynamic source compilation**: When `ScriptConfig.Dynamic.Enable` is true, Roslyn compiles `.cs` files directly from the configured source directory — enables hot-reload without server restart

Scripts are initialized during world startup and ticked each update cycle via `scriptManager.Update(lastTick)`. Each script assembly can define:
- Entity scripts implementing `IOwnedScript<T>` for custom AI behavior
- Spell effect handlers for scripted spell behaviors
- Zone-specific event triggers and quest logic

## Message Handler Discovery & Registration

All message handlers are auto-discovered at startup via reflection — no manual registration required:

1. `Assembly.GetTypes()` scans all loaded assemblies (including Script projects)
2. Filter by types where any generic interface matches `IMessageHandler<,>`
3. Each handler is registered with `IMessageManager.RegisterMessageHandler(Type)` which maps the message opcode to a handler delegate
4. Handlers live in `Network/Message/Handler/` organized by domain subdirectories: Account, Authentication, Character, Chat, Costume, Entity, Event, Friendship, Group, Guild, Housing, Info, Item, Loot, Mail, Matching, Misc, Option, PublicEvent, and more

### Handler Categories (18+ subdirectories)
- **Entity**: `ClientActivateUnitHandler`, `ClientActivateUnitCastHandler`, `ClientEntityCommandHandler`, `ClientEntityInteractionHandler`, `ClientEntityTargetHandler` — core combat/interaction entry points
- **Character**: Character list management, appearance changes, buyback items
- **Housing**: All residence/housing interactions (edit mode, decor updates, community actions, visits)
- **Matching**: Queue operations, match voting, teleport to instance, leave queue
- **Guild**: Circle operations, invites, rank changes, community management

## Command System

Attribute-driven command framework with built-in parameter resolution and RBAC integration:

### Architecture
- `CommandManager`: Singleton with dedicated console input thread (disabled when running as Windows Service / systemd)
- Commands registered via `[Command]` attribute on classes implementing `ICommandHandler`
- Parameter converters implement `IParameterConvert` with `[Convert(Type)]` specifying the target type
- Command context: `ConsoleCommandContext` for direct console, `WebSocketCommandContext` for admin panel WebSocket

### Execution Model
1. **Direct (console thread)**: Console input parsed immediately via `HandleCommand()` — only when running in a terminal
2. **Delayed**: Commands from WebSocket or programmatic sources use `HandleCommandDelay()` which enqueues to `ConcurrentQueue<PendingCommand>` and processes on the world tick after all systems have updated

### Command Categories (30+)
Account, Achievement, Ban, Broadcast, Character, Currency, Disable, Door, Entitlement, Entity, GameTable, GenericUnlock, Guild, Help, House, Item, Location, Map, Movement, Path, Pet, Quest, RBAC, Realm, Reputation, Script, Spell, Story, Teleport, Title

### Command Resolution
Commands split input on spaces into `ParameterQueue`. Type conversion uses:
1. Registered converters from `[Convert]` attributes (discovered via reflection at startup)
2. Default fallbacks for common types (string, uint, int, etc.)
3. Parameter queues support breadcrumb trails for nested command help

## Inter-Service Messaging

WorldServer subscribes to 50+ message types via Rebus bus during `NetworkInternalHandlerHostedService.StartAsync()`. Message catalog:

| Source Service | Messages | Count |
|---|---|---|
| **Chat** | Channel actions, join results, member updates, text messages (channel/whisper), results | 7 |
| **Friendship** | Invite lists/removals, last online, level/location/nickname/note/status/presence updates, personal status, lists, removals, result notifications, type updates, added | 19 |
| **Group** | Action results, flags, loot rules, markers, max size changes, member add/flags/join/left/position/promote/realm/update/remove/request/results/stats, player invite/invite result, ready check started | 18 |
| **Player** | Group association updates, info responses | 2 |
| **Who** | Who response messages | 1 |

Messages are subscribed at startup — if a handler is missing or the message type is new, it will be silently dropped by Rebus.

## Admin Web Panel

Embedded ASP.NET Core admin console accessible via browser and WebSocket:

### REST API (`/api/admin/*`)
| Endpoint | Method | Description |
|---|---|---|
| `/server/status` | GET | Online player count, loaded map count |
| `/server/broadcast` | POST | Server-wide broadcast to all online players (with tier) |
| `/maps` | GET | All known worlds from game tables with player/entity counts and load status |
| `/loaded-maps` | GET | Currently loaded maps (overworld + instances) with entity/player counts |
| `/instances` | GET | Active instances with state, player/entity counts, unloading status |
| `/instances/{id}/unload` | DELETE | Unload a stuck instance by GUID |
| `/entities/{worldId}` | GET | All entities on a map, optionally filtered by type (Player, CreatureEntity, etc.) |
| `/entities/detail/{guid}` | GET | Full entity state: position, rotation, health, shields, level, faction, properties, combat data, threat list |
| `/entities/{guid}/display` | PUT | Change display appearance (Creature2DisplayInfo) |
| `/entities/{guid}/faction` | PUT | Set temporary faction on an online entity |
| `/accounts` | GET | Search accounts with pagination; returns roles, permissions, suspension status |
| `/accounts/{id}` | GET | Full account detail including all roles, permissions, active bans |
| `/accounts` | POST | Create new account (email + password → salt+verifier via Cryptography package) |
| `/accounts/{id}/ban` | POST | Ban account with optional duration (e.g., `7.00:00:00`) — disconnects if online |
| `/accounts/{id}/unban` | POST | End all active bans on an account |
| `/accounts/{id}/role` | PUT | Set primary role for an account |
| `/accounts/{id}/characters` | GET | List characters for an account with pagination |
| `/characters` | GET | Search/filter characters by name, accountId, online status with pagination |
| `/characters/{id}` | GET | Full character detail: items, quests (with objectives), spells, stats |
| `/characters/{id}/item` | POST | Add item to an online character's inventory |
| `/characters/{id}/teleport` | POST | Teleport an online character to world/position |
| `/characters/{id}/level` | PUT | Set level for offline characters (DB) or live update online players |

### WebSocket Console (`/ws/commands`)
- JSON message format: `{"message": "command text"}`
- Commands are delegated to `CommandManager.HandleCommandDelay()` with a `WebSocketCommandContext` that writes responses back over the same WebSocket connection
- Supports all command categories (30+) and parameter converters

### Admin Web Controllers
- `AdminController`: Account/character CRUD, entity inspection/modification, server status/broadcast
- `GameTableController`: Query game table entries by type for admin reference

## Work Guidance

### Adding a New Message Handler
1. Define the message class (client→server: `Client*`, server→client: `Server*`) in `Network.World/Message/Model/` or `Static/`
2. Mark with `[Message(Opcode)]` attribute — opcodes must be unique within protocol direction
3. Implement `IMessageHandler<ISession, TMessage>` where session is `IWorldSession` and message is your typed class
4. Place handler in the appropriate subdirectory under `Network/Message/Handler/` (follow existing domain grouping)
5. No registration needed — auto-discovered via reflection at startup

### Adding a New Command
1. Create or extend a category class that implements `ICommandCategory` (inherits from `CommandCategory`)
2. Use `[Command("name", Description, Permission)]` on individual handler methods for inline commands
3. For complex parameters: implement `IParameterConvert` with `[Convert(Type)]` attribute and place in `Command/Convert/` directory
4. Commands that modify game state should check RBAC permissions — use the `Permission` enum from Game.Static.RBAC
5. Use `HandleCommandDelay()` for commands invoked from non-console contexts (WebSocket, programmatic)

### Modifying Online Entity State
- Always work through entity references obtained from the admin panel queries or message handler context
- Display changes (`entity.DisplayInfo`) and faction changes (`SetTemporaryFaction()`) are live — no DB persistence required
- Combat state modifications must respect the world tick ordering — use `HandleCommandDelay` if modifying during a non-tick context

### World Tick Ordering Critical Dependencies
The update callback chain has strict ordering requirements:
- **Residence** depends on **Guild** (guild data must be current before residence updates)
- **Commands** must run last (after all systems have updated to avoid race conditions)
- **Network/Map** must come first (entity positions must be known before other systems query them)
- Altering this order breaks game logic — document any new subsystem integration in the Initialization Order section

### Thread Safety
- Game loop runs on a single world thread; all tick callbacks execute sequentially
- Message handlers may be invoked from network I/O threads — use `HandleCommandDelay` for any state modification that touches game systems
- Admin REST API endpoints query live game state — expect null references during initialization/shutdown (guarded by try/catch in production code)

## Verification

- **Build**: `dotnet build Source/NexusForever.slnx` — full solution including all script zones
- **World tick ordering**: Test new subsystem integration by verifying correct update sequence — look for NullReferenceException during startup that indicates a dependency was initialized too late
- **Script loading**: Set `ScriptConfig.Enable=true` and verify scripts load without errors in server log; hot-reload test with `Dynamic.Enable=true` changes
- **Login queue**: Test max-player enforcement by setting `MaxPlayers=0` and verifying new connections are queued or rejected with `ServerQueueStatus`
- **Command system**: Verify RBAC integration by testing commands with/without appropriate permissions — both should return the same generic error message to prevent command scraping
- **Admin panel**: Access REST endpoints directly; verify WebSocket console at `/ws/commands` sends/receives JSON command messages correctly

## Child DOX Index

None. WorldServer is a single project with no sub-domains requiring their own AGENTS.md files.
