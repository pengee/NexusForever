# Game Domain

Covers all game logic: combat, spells, quests, entities, housing, maps, and related systems.

## Purpose

Define rules for `NexusForever.Game`, `NexusForever.Game.Abstract` (interfaces), and `NexusForever.Game.Static` (enums/static data). This is the most complex domain with 29+ subsystem directories, 100+ interface definitions in Abstract, and 65+ enums/constants in Static. DI registration occurs via `ServiceCollectionExtensions.AddGame()` which wires all subsystems through `AddSingletonLegacy`.

## Ownership

- **Game.Abstract**: All interfaces for game systems — other domains depend on this project, never the concrete Game implementation
- **Game.Static**: Enums, constants, and static lookup tables derived from WildStar server build 16042
- **Game**: Concrete implementations of all game mechanics — combat, spells, quests, housing, guilds, mail, matching, public events, story, AI turret behavior

## Local Contracts

- Interfaces in `NexusForever.Game.Abstract` must be minimal and stable; avoid breaking changes once published
- Enums in `NexusForever.Game.Static` mirror WildStar server build 16042 — document mapping source when deviating from game files
- Game systems are registered via `ServiceCollectionExtensions` DI extensions with `AddSingletonLegacy` for legacy service provider compatibility
- Entities implement defined interfaces from the Abstract project

## Entity Hierarchy & Lifecycle

Entities follow a strict inheritance chain — understanding it is essential for any subsystem work:

- **IGameObject** → **IWorldEntity** (shared by all) → **IGridEntity** (spatially-aware on maps) → **IUnitEntity** (combat-capable, casts spells, participates in combat)
  - **Player**: Full player character — owns spell manager, quest manager, buff/currency/title managers, inventory, housing access. Inherits from UnitEntity.
  - **CreatureEntity**: NPC/monster with AI behavior via script integration. Uses `DefaultCombatAI` from Script.Main by default.
  - **AiTurretEntity**: Static defensive entity with its own threat/combat logic.
- Non-unit entities: Camera, Chest, Door, Collectable, Corpse, Platform, Taxi, Trap, Trigger — all implement IWorldEntity or IDestructibleEntity

### Lifecycle Flow
1. **Creation**: `IEntityManager` creates instances via `IEntityFactory.CreateEntity<T>()`
2. **Initialization**: Each entity's `Initialise()` sets up position, stats, and registers with EntityManager
3. **Add to Map**: `IMap.EnqueueAdd(entity, position)` — grid-based spatial placement with collision validation
4. **Update Tick**: Entities receive `IUpdate.OnUpdate(double delta)` per game loop tick — buffs tick, spells execute, AI ticks
5. **Remove from Map**: `RemoveFromMap()` → despawn packets sent to clients → EntityManager cleanup

### Key Managers (singleton-registered)
- `EntityManager` / `PlayerManager`: Global entity tracking and player list management
- `BuffManager`: Buff/debuff lifecycle with expire callbacks and tick logic
- `SpellManager` / `GlobalSpellManager`: Spell registration, casting coordination, casting ID generation
- `LootManager`, `CurrencyManager`, `CostumeManager`, `TitleManager`: Player-specific resource managers

### Player Persistence
Player persistence is split across databases: auth data (account), character data (stats, inventory, quests). Load on login via `Database.Character.CharacterContext`. Save triggers on location changes (flagged by `PlayerSaveMask`), logout, and periodic checkpoints.

## Combat System

Combat is event-driven through message handlers in WorldServer (`ClientActivateUnitHandler`, `ClientActivateUnitCastHandler`) which delegate to game systems.

### Key Types
- **DamageCalculator** (`IDamageCalculator`): Central damage computation — property scaling from stats (Strength/Dexterity/Tech/Magic/Wisdom/Stamina), armor mitigation formula, crit/glance/multi-hit/resist/shield calculations. Uses GameFormula entries from tbl files for formula lookups.
- **ThreatManager**: Aggro management with threat generation/conversion/depletion rules — implements `IThreatManager` (defined in Abstract.Combat)
- **ProcManager** (`IProcManager`): On-damage/on-crit triggers — evaluates proc chains via `IProc` implementations and `ProcTriggerType` enum

### Spell Effect Pipeline
1. Spell cast begins → prerequisites checked via Prerequisite system
2. Effects dispatched via `[SpellEffectHandler(SpellEffectType.X)]` attribute on static methods in `Spell/SpellEffectHandler.cs`
3. Each effect type maps to one handler method — see `Static/Spell/SpellEffectType.cs` for all ~50+ registered types: Damage, Heal, Absorption, HealingAbsorption, VitalModifier, CCStateSet, SpellDispel, SpellForceRemove, ModifyInterruptArmor, CCStateBreak, Proc, Stealth, RemoveStealth, ShieldOverload, Transference, ModProperty, AddSpell, AddSpellEffect, SuppressSpellEffect, HealShields, DamageShields
4. Periodic effects create buffs with tick callbacks that replay the effect

### Crowd Control (CC) System
- CC states defined in `Static/Combat/CrowdControl/CCState.cs` — stun, root, silence, charm, etc.
- Diminishing returns tracked per category via duration modifiers — prevents infinite CC chains
- Interrupt armor consumed on CC application; infinite interrupt armor (`uint.MaxValue`) blocks all CC
- Movement CC vs Spell CC differentiated by `HasMovementCC()` / `HasSpellCC()` checks on IUnitEntity

### Combat Ratings → GameFormula Mapping
Key properties map to specific formula entries in tbl files:
- Armor mitigation: Formula 1234 (uses victim level)
- Crit chance/severity: Formulas 1231/1232
- Deflect/Avoid: Formula 1235
- Glance chance/amount: Formulas 1271/1245

When adding new ratings, add the formula entry mapping in `DamageCalculator.GetRatingPercentMod()`.

## Spell System

Spells are data-driven from `.tbl` game tables (Spell4, Spell4Effects, CCStates entries). Each spell has a status-based lifecycle managed by `ISpell`.

### Spell Lifecycle States
1. **Initiating** → Cast prerequisites checked → transitions to...
2. **Casting** → Client cast bar shown, interruptible if not immune → transitions to...
3. **Executing** / **Channeled** → All effects dispatched immediately (casting) or ticked over duration (channeled) → transitions to...
4. **Finished** → Spell disposed, cleanup callbacks fire

### Effect Registration Pattern
- Effects are registered via `[SpellEffectHandler(SpellEffectType.X)]` attribute on static methods in `Spell/SpellEffectHandler.cs`
- Method signature: `static void HandleEffectX(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)`
- Auto-discovered at startup by scanning assembly for the attribute
- Each effect type maps to one method — see `Static/Spell/SpellEffectType.cs` for all registered types

### Spell Parameters & Propagation
- `ISpellParameters` carries parent/root spell references (for proxy spells) and taxi node targets
- Proxy spells (`SpellEffectType.Proxy`) re-cast with a different spell4Id while preserving the parent chain
- `AddSpell`/`RemoveSpell` effects add/remove temporary spell grants via buff expire callbacks

### Telegraphs & Anchors
- Spell casts can create telegraphs (area-of-effect ground targets) tracked in `List<ITelegraph>` within the spell instance
- Anchor entities (e.g., summoned objects, traps) track spatial relationships to caster for positioning effects

### Known Gotchas
- `SpellEffectHandler` methods are **static** — cannot use dependency injection directly. Use `LegacyServiceProvider.Provider.GetService<T>()` factory pattern when needed (documented in TODOs). This is a known limitation being worked toward fixing.
- Full-screen effects need manual `EnqueueEvent(SpellEvent)` for timed completion since executing spells finish immediately without timer.

## Map & Zone Management

Zones use a spatial grid system for entity caching and proximity queries. `IMapFactory` creates instances from `.nfmap` files generated by MapGenerator.

### Key Types
- **ZoneMap**: Persistent overworld zone — loaded at server start, entities persist across player visits
- **InstanceMap**: Ephemeral instance zones (dungeons/raids) — created on demand, disposed when empty
- **IMapGrid** / **MapCell**: 2D spatial subdivision for efficient entity lookups by position
- **EntityCacheManager**: Maintains per-cell entity lists with grid action queue (`IGridActionAdd`, `IGridActionRelocate`, `IGridActionRemove`)

### Grid Actions
All map modifications go through a queued pending system:
1. Action enqueued to the appropriate `GridActionPending` list
2. Processed during update tick — adds entities to cell lists, updates cache indices
3. Redundant operations coalesced (`GridActionRelocate` after `GridActionAdd` for same entity)

### Zone Transitions (Teleportation)
- Players use `IPlayer.TeleportTo(mapId, position)` which validates entry via `CanEnter()` before removing from old map and adding to new one
- Taxi nodes (`TaxiNodeEntry`) trigger rapid transport with rotation preservation
- Instance portals create new instances if needed, or join existing ones

### Search System
- `Search` directory contains indexing for entity lookups by criteria (nearest target, specific GUID, area scans)
- Used by spell targeting, AI aggro selection, and admin commands

## Cinematics

Cinematics are scripted camera/actor sequences for cutscenes and keyframe events. `IGlobalCinematicManager` manages the global state machine.

### Key Types
- **CinematicBase / ICinematicBase**: Abstract base for all cinematic types with factory pattern via `ICinematicFactory`
- **Camera**: Controls viewport position, target tracking, transitions between shots
- **Actor / IActor**: Represents a visible entity in the scene (can be player, NPC, or invisible)
- **FlagsKeyframe**: Discrete keyframes that set/clear flags on actors at specific timestamps

### Actor Visibility System
- `IActorVisibility` tracks per-camera actor visibility — actors can be hidden from some cameras but not others
- Used for split-screen cinematics and selective entity culling during cutscenes

## Guild System

Guilds are persistent social entities with ranks, circles (teams), communities (guild alliances), and arena teams.

### Data Model
- **GuildBase**: Shared data between regular guilds and arena teams — name, standard, creation info
- **Guild / ArenaTeam**: Full guild instance with members, operations, invites
- **Circle**: Guild team (up to 10 per guild) for grouping players within a guild
- **Community**: Guild alliance/coalition structure across multiple guilds
- **WarParty**: Faction-wide temporary alliance during world events

### Operations System
- `GuildOperationHandlerAttribute` registers operation methods on `IGuildBaseOperation` implementations
- Operations are action handlers: rename, disband, add member, remove member, etc.
- Each returns `GuildResultInfo` with success/failure and error code

## Housing (Residence) System

Housing uses a residence-based model where players own plots, place decor/plants, and grant access to communities.

### Key Types
- **Residence**: Player-owned housing instance with entrance portals, decor items, plants
- **Plot**: The land parcel that defines the residence's boundaries within a zone
- **GlobalResidenceManager**: Manages all active residences globally
- **PublicCommunity / PublicResidence**: Database-backed repositories for persistence

### Placement Rules
- Decor/plants must be placed within plot bounds — validated against zone geometry before committing
- Residence entrances create teleport portals to/from housing instances
- Plots are managed separately from the residence itself (a plot can exist without an active residence)

## Other Subsystems

### Quest / Story / Achievement
- **Quest**: `GlobalQuestManager` tracks all active quests; per-character tracking via `QuestInfo`/`QuestObjective` with objective completion callbacks
- **Story**: `StoryBuilder` constructs story messages for cutscene-style narration, triggered by quest/story events
- **Achievement**: Three-layer architecture — `BaseAchievementManager`, `CharacterAchievementManager` (per-player progress), `GuildAchievementManager` (guild-wide progress). All registered via DI.

### Matching System
- `MapEntrance`: Zone entry points with matching gate logic that validates group/queue eligibility
- `MatchingDataManager`: Queue management and matchmaking logic for player groups
- `MatchingMap`: Instance maps created specifically for matched content instances

### Chat System
- `GlobalChatManager` manages channel subscriptions, message broadcasting, and format dispatch
- `Format` directory contains specialized message builders for different chat channels (global, trade, guild, whisper)

## Work Guidance

### Adding a New Spell Effect Type
1. Add the new enum value in `Static/Spell/SpellEffectType.cs`
2. Create/update handler method in `Spell/SpellEffectHandler.cs` with `[SpellEffectHandler(NewType)]` attribute
3. Handler receives `ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info` — read effect parameters from `info.Entry.DataBitsXX` fields
4. For periodic effects: create a buff via `target.BuffManager.AddBuff()` and set its tick callback to replay the effect

### Adding Combat Ratings / Formulas
1. Add the enum value in `Static/Entity/Property.cs` if it's a new stat/rating
2. Add the GameFormula entry (tbl file) — use consistent numbering with adjacent formulas
3. Update `DamageCalculator.GetRatingPercentMod()` and `GetBasePercentMod()` with mapping switch cases

### Modifying Entity Behavior
1. Prefer script-based behavior overrides via `IScript` / `IOwnedScript<T>` pattern (see Script.Main AI systems)
2. Direct code changes to entity base classes should be rare — only for core lifecycle or universal mechanics
3. Always check if the behavior can be handled by spell effects + buffs before adding new entity properties

### Buff & CC Interaction Rules
- CC state application checks diminishing returns before applying duration
- Interrupt armor is consumed sequentially; infinite armor (`uint.MaxValue`) blocks all CC
- Movement CC and Spell CC are checked independently — an entity can have both simultaneously
- Dispel effects (`SpellDispel`) remove buffs matching criteria; ForceRemove targets specific spell4Ids

### Thread Safety
- Entity managers use concurrent collections internally for thread-safe access during game loop ticks
- Player save operations serialize through the logout/save manager to prevent concurrent DB writes
- Spell effect handlers run on the entity tick — assume single-threaded unless using explicit threading primitives

## Verification

- `dotnet build Source/NexusForever.slnx` — compiles game domain with all dependencies
- Combat balance requires manual testing with controlled scenarios (dummies, known HP values) — verify DamageCalculator outputs match expected formula results
- Spell effects must be tested for both casting and periodic/tick behavior — use `SpellEffectType.FullScreenEffect` duration tracking as a reference for timed spell completion
- Zone loading verified by entering zones via WorldServer with entity spawning checks (use admin console to spawn test entities)
- No automated tests exist; rely on integration testing via WorldServer

## Child DOX Index

None. Game.Abstract and Game.Static are subdirectories of the same project structure without separate AGENTS.md files.
