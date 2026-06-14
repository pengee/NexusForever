# Database Domain

Covers all Entity Framework Core database contexts, migrations, and repositories for the MySQL/MariaDB backend.

## Purpose

Define rules for `NexusForever.Database` (EF Core base), per-domain context projects (`Database.Auth`, `Database.Character`, `Database.Chat`, `Database.Friendship`, `Database.Group`, `Database.World`, `Database.Query`), and shared database infrastructure. The domain implements a hybrid architecture: three databases use the reflection-discovered `IDatabase` wrapper pattern (Auth, Character, World) while others register DbContext directly via DI extensions (Chat, Friendship, Group). All 7 domains share explicit type mapping conventions for MySQL/MariaDB compatibility.

## Ownership

- **Database**: Base EF Core provider (`Pomelo.MySql`), common interfaces (`IDatabase`, `IDatabaseManager`, `IWrappedModel<T>`), database attribute and provider abstraction, connection string configuration
- **Database.Auth**: 15 models — user accounts, costumes, currencies, entitlements, keybindings, RBAC permissions/roles/suspensions. Implements `[Database(DatabaseType.Auth)]` wrapper with `Save(Action<AuthContext>)`, `GetAccountByEmailAsync()`, session token lookups via GameToken and SessionKey
- **Database.Character**: 41 models — character stats, appearances, costumes, buffs, currencies, mail attachments, equipment, tradeskill materials. Implements `[Database(DatabaseType.Character)]` with three `Save()` overloads (Action<T>, single IDatabaseCharacter, IEnumerable<IDatabaseCharacter>) for flexible persistence patterns
- **Database.Chat**: 6 models — chat channel membership, internal messaging, character-specific chat state. Registered directly via DI (`AddDbContext<ChatContext>`), no explicit DB wrapper — ChatServer manages context lifetime
- **Database.Friendship**: 18 models — account/character friend lists, invite states (pending/inverse), persistent presence tracking. Same DI pattern as Chat — `AddDbContext<FriendshipContext>`, registered via `ServiceCollectionExtensions`
- **Database.Group**: 11 models — party/raid groups, leader election, member state, markers, invites, request queue. DI-scoped DbContext with `GroupContext`. GroupServer manages context lifetime
- **Database.World**: 20 models — entity persistence (stats, events, splines, vendors, scripts), map entrances, store offer system (categories, groups, items, prices), tutorials, version tracking. No repository layer — `WorldDatabase` exposes query methods directly returning `ImmutableList<EntityModel>` with `.AsSplitQuery()` for complex includes
- **Database.Query**: Read-optimized projections for cross-service reporting. Registers `CharacterRepository`, `QueryRepository`, and `QueryExpressionBuilder`. Used by WorldServer for character list queries

## Local Contracts

- **DbContext patterns vary by domain**: Auth/Character/World use `[Database(DatabaseType.X)]` attribute with reflection-discovered `IDatabase` wrapper managed by `DatabaseManager.Initialise()`. Chat/Friendship/Group register directly via `ServiceCollectionExtensions.AddDbContext<T>()`.
- **Model type annotations are mandatory**: Every property uses explicit `.HasColumnType("type signed")` (e.g., `"tinyint(3) unsigned"`, `"bigint(20)"`). This is required for MySQL/MariaDB compatibility and migration consistency.
- **Enums stored as integers**: All enum properties use `EnumToNumberConverter<T, byte>` — never string-based storage. Check `NexusForever.Game.Static` for enum value mappings.
- **Composite primary keys** use anonymous objects: `.HasKey(e => new { e.Id, e.Property })`. Primary key column order in the compound matters for indexes.
- **One-to-one relationships** use explicit FK constraint names via `.HasConstraintName("FK__table_col__other_table_col")` pattern — required for correct migration generation.

## Work Guidance

### Adding a New Model (7-step process)

1. Create a new class in `Model/` directory following naming convention: `<Entity>Model.cs`. Use PascalCase with `Model` suffix.
2. Define properties using value types where possible (`uint`, `int`) — match WildStar server build 16042 field layouts exactly.
3. Apply **every** property annotation: `.HasColumnName("camelCaseColumnName")`, `.HasColumnType("type signed")`, and `.HasDefaultValue(value)` if applicable. Omitting these causes migration mismatches between MySQL and MariaDB.
4. Set up relationships using explicit constraint names: `.HasForeignKey<X>(e => e.RefId).HasConstraintName("FK__table_col__ref_table_id")`. Use `EnumToNumberConverter<T, byte>` for enum properties.
5. Add the model to the appropriate DbContext's `DbSet<T>`, then run: `dotnet ef migrations add <MigrationName> --project NexusForever.Database.<Domain> --startup-project Source/NexusForever.Aspire.AppHost`
6. Test migration in both Up and Down directions — verify column addition/removal preserves data via explicit `.HasDefaultValue()` defaults.
7. If the domain uses `[Database]` wrapper, add query methods to the IDatabase implementation; if DI-scoped, create a repository class under `Repository/`.

### Save Method Selection Guide

| Pattern | Method | When to Use | Example |
|---|---|---|---|
| **Action<T>** | `Save(Action<AuthContext> action)` | Simple single-context operations with atomic commit. Auth domain standard pattern. | Creating an account, updating permissions |
| **Single entity** | `Save(IDatabaseCharacter character)` | Individual entity persistence where the entity manages its own EF tracking state via `.Save(context)`. | Saving a single character's stat changes during gameplay |
| **Bulk entities** | `Save(IEnumerable<IDatabaseCharacter> entities)` | Multiple entities that share transaction context. Each entity calls Save() individually within same context scope. | Bulk character equipment update, multi-item mail attachment save |

### Repository Layer Conventions

- Repositories are thin wrappers around DbContext with async methods — no business logic in repositories
- **Auth**: `AccountRepository` (email/id lookups), `ServerRepository` (simple CRUD)
- **Character**: `CharacterRepository` (bulk entity loading, pagination via `GetCharactersForAccountAsync`)
- **World**: No repository layer — `WorldDatabase.GetEntities()` returns pre-loaded entities with `.AsNoTracking()` for read-only operations. Complex includes handled by private `EntitiesInclude()` method that chains 7+ `.Include()` calls
- **Query**: `CharacterRepository` (cross-character queries), `QueryRepository`, `QueryExpressionBuilder` (dynamic query composition)

### Migration Conventions

- Standard EF Core pattern: Up/Down methods with explicit type annotations in every migration
- Naming format: `YYYYMMDDHHMMSS_Name.cs` — timestamps enforce ordering automatically
- **Never use `.DropColumn()` or change column types in production migrations** without a data-preserving script. Test migration impact on existing data first.
- Auth and Character databases call `.Migrate()` at startup (in their IDatabase implementations). Chat/Friendship/Group/World migrations are handled by the Aspire Database.Migrations runner only — do not add StartupMigrate calls to these contexts.

### Thread Safety & Query Patterns

- **Always use `.AsNoTracking()` for read-only queries** — entities loaded via `GetEntities()`, repository lookups should never participate in change tracking
- **Use `.AsSplitQuery()` for entity graphs with 1-to-N relationships** (e.g., Entity → EntityStat, Entity → EntityVendorItem). Without it, EF Core generates cartesian product queries that are orders of magnitude slower.
- **ImmutableList returns from WorldDatabase**: All bulk query methods return `ImmutableList<T>` to prevent accidental mutations after the DbContext is disposed.

### Retry Policy & Connection Resilience

All contexts use automatic retry on transient failures: `EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)`. This applies to all queries and saves — no need for manual retry logic in application code. The 5-retry policy with 10-second backoff handles brief MySQL connection drops during network partitions or failover events.

## Verification

- `dotnet build Source/NexusForever.slnx` — compiles all database projects
- Migrations verified by running `NexusForever.Aspire.Database.Migrations` against a fresh MySQL instance and confirming all 7 databases reach the latest migration state
- WorldDatabase query performance: verify `.AsSplitQuery()` usage on entity graphs with multiple 1-to-N includes (EntityStat, EntityVendorItem, etc.) to prevent cartesian explosion
- Cross-domain consistency: test that `[Database]` managed contexts (Auth/Character/World) and DI-scoped contexts (Chat/Friendship/Group) both apply migrations correctly in a running Aspire host

## Child DOX Index

None. All per-domain context projects share the same structure and complexity level.
