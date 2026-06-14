# AGENTS.md

## Project

WildStar (build 16042) server emulator in C# / .NET 10. Single solution at `Source/NexusForever.slnx` (also `Source/NexusForever.sln`). Central package management via `Source/Directory.Packages.props`.

## Build

```bash
dotnet build Source/NexusForever.slnx
```

Requires .NET 10 SDK (C# 14). No test projects exist in the solution.

## DOX Framework

This project uses a hierarchical documentation system called **DOX** (Document Ownership eXchange), defined in `Source/AGENTS.md`. Every AGENTS.md file is a binding work contract for its subtree — source materials, instructions, and durable docs must remain understandable from the nearest applicable AGENTS.md plus every parent above it.

When working on this project:
1. Read `Source/AGENTS.md` first to understand the full DOX chain
2. Identify every file or folder you expect to touch
3. Walk from the repository root to each target path and read every AGENTS.md found along that route
4. If a parent AGENTS.md lists a child AGENTS.md whose scope contains the path, read that child and continue from there
5. Use the nearest AGENTS.md as the local contract; use parent docs for repo-wide rules
6. If docs conflict, the closer doc controls local work details — no child doc may weaken DOX core rules

After making meaningful changes, run a **DOX pass**: update the closest owning AGENTS.md (and any affected parents/children), refresh every Child DOX Index, and remove stale or contradictory text. See `Source/AGENTS.md` for full read-before-editing and closeout procedures.

### Child DOX Index

| Domain | Path | Scope |
|---|---|---|
| Auth Servers | `NexusForever.AuthServer/AGENTS.md` | AuthServer (login), StsServer (tickets) |
| World Server | `NexusForever.WorldServer/AGENTS.md` | Game loop, entities, zones, commands, message handlers |
| Game Domain | `NexusForever.Game/AGENTS.md` | Combat, spells, quests, housing, matching, entity AI |
| Network Domain | `NexusForever.Network/AGENTS.md` | Packet infrastructure, Auth/Sts/World/Internal protocol layers |
| Database Domain | `NexusForever.Database/AGENTS.md` | EF Core contexts, migrations, repositories (6 databases) |
| Script Domain | `NexusForever.Script/AGENTS.md` | Roslyn runtime compiler + 7 zone script projects |
| API Domain | `NexusForever.API.Account/AGENTS.md` | REST endpoints, shared models, client proxies for Account & Character |
| Microservices | `NexusForever.Server.Character/AGENTS.md` | Character, Chat, Friendship, Group microservices |

Infrastructure projects (`Shared`, `GameTable`, `IO`, `Cryptography`) and Aspire orchestration projects (`Aspire.AppHost`, `Aspire.Database.Migrations`) are covered by this file and do not have separate DOX files.

## Architecture

All source lives under `Source/`. The project is a distributed microservice system orchestrated via .NET Aspire.

### Executables (servers/services)

| Project | Role |
|---|---|
| `NexusForever.AuthServer` | Authentication (port 23115) |
| `NexusForever.StsServer` | STS ticket service |
| `NexusForever.WorldServer` | World/game server (port 24000) |
| `NexusForever.Server.Character` | Character microservice |
| `NexusForever.Server.ChatServer` | Chat microservice |
| `NexusForever.Server.Friendship` | Friendship microservice |
| `NexusForever.Server.GroupServer` | Group microservice |
| `NexusForever.Aspire.AppHost` | Aspire orchestration host |
| `NexusForever.Aspire.Database.Migrations` | Database migration runner |
| `NexusForever.ClientConnector` | Client-side connector utility |
| `NexusForever.MapGenerator` | Extracts game tables / generates maps from WildStar client archives |

### Core libraries

#### Game systems

| Project | Purpose |
|---|---|
| `NexusForever.Shared` | Base config, DI, logging (NLog) |
| `NexusForever.Game` | Combat, spells, quests, housing, guilds, mail, matching, public events, AI turret behavior |
| `NexusForever.Game.Abstract` | Interfaces for game systems |
| `NexusForever.Game.Static` | Enums and static game data types |
| `NexusForever.GameTable` | WildStar game table (.tbl) loader |

#### Entity & world simulation (in Game project)

- **Entity**: Creature, player characters, NPCs, AI turrets, corpses, cameras, collectables, destructible objects
- **Map**: Zone maps, instance maps, grid management, entity caching, search indexing
- **Cinematic**: Actor visibility, camera controllers, keyframe system, spline-based transitions
- **Guild**: Teams (circles), communities, global guild management

#### Network & database

| Project | Purpose |
|---|---|
| `NexusForever.Network` | Packet infrastructure (`GamePacketReader/Writer`, sessions, network GUIDs) |
| `NexusForever.Network.Auth/Sts/World/Internal` | Protocol-specific message definitions and session handling |
| `NexusForever.Cryptography` | Auth crypto |
| `NexusForever.Database` | EF Core base (MySQL via Pomelo) |
| `NexusForever.Database.Auth/Character/Chat/Friendship/Group/World/Query` | Per-domain DB contexts with migrations and repositories |

#### Scripting & API

| Project | Purpose |
|---|---|
| `NexusForever.Script` | Runtime C# script compilation (Roslyn) — loaded by WorldServer at startup |
| `NexusForever.Script.*` (7 projects) | Zone scripts (Alizar, Arcterra, Farside, Instance, Isigrol, Main, Olyssia) |
| `NexusForever.API` | Shared API models for account/character REST endpoints |
| `NexusForever.API.Account/Character` | REST API services with endpoint definitions |
| `NexusForever.API.Account.Client/Character.Client` | HTTP client proxies for inter-service calls |

#### Infrastructure utilities

| Project | Purpose |
|---|---|
| `NexusForever.IO` | File I/O helpers |

### Key patterns

- **Message handlers**: Implement `IMessageHandler<ISession, TMessage>` — auto-registered via DI. Handlers live in `WorldServer/Network/Message/Handler/`.
- **Command system**: WorldServer has a full command framework with categories (`CommandManager`, shared handlers). Command implementations live under `WorldServer/Command/Handler/`.
- **Service registration**: Each library has a `ServiceCollectionExtensions` class with DI extensions.
- **Config**: Each server reads from a `*Server.example.json` (copy to `*Server.json` for local use). DB connections use MySQL connection strings.
- **Logging**: All servers use NLog (`nlog.config` per project).
- **Inter-service messaging**: Rebus over RabbitMQ or Azure Service Bus (configurable per `Network.Internal.Broker`).
- **Scripts**: Roslyn-compiled C# scripts loaded at runtime. Script projects are build dependencies of WorldServer.

### Admin web panel

WorldServer includes an embedded admin console:
- Controllers in `WorldServer/Web/Controllers/` (`AdminController`, `GameTableController`)
- WebSocket notifications via `WorldServer/Web/Middleware/WebSocketController.cs`
- Frontend at `WorldServer/wwwroot/console.html`

## Database

- MySQL/MariaDB with EF Core migrations via Pomelo provider.
- 6 databases: `nexus_forever_auth`, `nexus_forever_character`, `nexus_forever_chat`, `nexus_forever_friendship`, `nexus_forever_group`, `nexus_forever_world`.
- Migrations live in `NexusForever.Database.*/Migrations/`.
- Run migrations via `NexusForever.Aspire.Database.Migrations` or `dotnet ef` against individual DB projects.

## Game data files

- `.tbl` (game tables) and `.nfmap` (map files) are generated and gitignored.
- `MapGenerator` extracts tables (`--e`) or generates maps (`--g`) from a WildStar client patch directory.
- WorldServer has optional MSBuild targets: `ExtractGameTableFiles.targets` (set `ExtractGameTables=true`) and `GenerateBaseMapFiles.targets` (set `GenerateMapFiles=true`).

## Conventions

- `Obsolete/` directory is legacy code, not part of the solution.
- Nullable reference types are **disabled** in most projects (`<Nullable>disable</Nullable>`).
- `ImplicitUsings` enabled in newer projects, disabled in older ones (e.g., `Shared`, `Database`).
- PRs target the `game_rework` branch (not `master` or `develop`).
- Many projects include a `Design/` directory for design-time-only files (VS metadata generation).
- Planning artifacts may appear in `.opencode/plans/`.
