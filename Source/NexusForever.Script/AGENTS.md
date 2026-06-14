# Script Domain

Covers zone scripts compiled at runtime via Roslyn. Each script project represents a distinct game zone or area with custom AI, events, and logic.

## Purpose

Define rules for `NexusForever.Script` (runtime compilation engine) and the 7 zone-specific script projects: `Script.Alizar`, `Script.Arcterra`, `Script.Farside`, `Script.Instance`, `Script.Isigrol`, `Script.Main`, `Script.Olyssia`. These are loaded dynamically by WorldServer at startup via a Roslyn-based compilation pipeline that supports both pre-compiled DLLs and hot-reload from source files.

## Ownership

- **Script**: Roslyn runtime compiler (`CSharpCompiler`), script discovery engine, assembly loading, ScriptManager singleton, template interface hierarchy (hook system), event system. ~60 source files, comprehensive engine.
- **Script.Alizar**: Alizar zone scripts — Everstar Grove, Northern Wilds areas
- **Script.Arcterra**: Arcterra zone scripts
- **Script.Farside**: Farside zone scripts
- **Script.Instance**: Instanced content (dungeons, raids) with dynamic spawning and encounter logic. Uses `EventBaseContentMapScript`/`EventBasePvpContentMapScript` base classes for public event coordination.
- **Script.Isigrol**: Isigrol zone scripts
- **Script.Main**: Main world zones — AI system (`DefaultCombatAI`, 342 LOC), tutorial quest flow, hub cities, open-world content. Largest script project (~33 source files).
- **Script.Olyssia**: Olyssia zone scripts

## Local Contracts

- Each script project produces a DLL via `NexusForever.Script.*.csproj` that is loaded by WorldServer at startup and referenced as a build dependency.
- Scripts are organized by in-game area/zone directories (e.g., `EverstarGrove/`, `Tutorial/`) within each script project.
- **Template interfaces use C# default methods** (`{ }`) — scripts only override the lifecycle hooks they need, no abstract method requirements.
- Script instances are created via `ActivatorUtilities.CreateInstance(serviceProvider)` — full constructor injection works for game services, event factories, and managers.

## Work Guidance

### Writing a New Entity Script

1. Create a class in your script project under an area subdirectory (e.g., `MyZone/Scripts/MyCreatureScript.cs`)
2. Choose the appropriate template interface from the hierarchy below:
   - **`IGridEntityScript`** → Tick + grid events (OnAddToMap, OnRemoveFromMap, OnEnterRange/OnExitRange)
   - **`IWorldEntityScript`** → Position commands, zone enter (`OnEnterZone`), activation (`OnActivate`)
   - **`IUnitScript`** → Threat list changes, death/respawn hooks (inherits from INonPlayerScript → IUnitScript)
   - **`IPlayerScript`** → Login/logout lifecycle
3. Apply filter attributes to control which entity receives this script:
   - `[ScriptFilterCreatureId(1234)]` — matches by creature ID (most common for NPC scripts)
   - `[ScriptFilterOwnerId(id)]` — matches by owner/player ID (instance/arena scripts)
   - `[ScriptFilterActivePropId(id)]` — matches by property IDs (housing scripts)
   - `[ScriptFilterDefault]` — fallback when no other filter matches (used for generic AI)
4. Implement only the lifecycle methods you need: `OnLoad()`, `OnUnload()`, tick (`Update()`), and any entity-specific hooks. All have empty default implementations.
5. If your script needs game services, inject them via constructor — use `ActivatorUtilities.CreateInstance` which resolves from WorldServer's DI container. Never call `IServiceProvider.GetService()` directly in scripts.

### Event System Usage

- Use **`IScriptEventManager`** (injected via DI) to schedule delayed events: create an `IPendingScriptEvent`, register a subscriber with `OnScriptEvent += OnEvent`.
- Pre-built event types: `EntitySayEvent` (chat output), `EntityRandomMovementEvent` (movement).
- Events fire on the world tick thread — do not block or perform expensive operations in event handlers.

### Hot-Reload Workflow

When `ScriptConfig.Dynamic.Enable = true`, changes to `.cs` source files trigger a filesystem watcher that sets a reload flag. The next world tick processes pending reloads: ScriptManager.Unload() → GC.WaitForPendingFinalizers loop (25 iterations max) → CSharpCompiler compiles updated sources → Re-initializes all matching script collections.

- **Compilation errors**: `CSharpCompiler` throws `CompileException` with diagnostic strings — log and abort reload, leave the old assembly loaded.
- **Use `/script info`** command to see per-assembly stats (loaded types, instance counts) for debugging.
- **Use `/script reload <assembly> Source`** to manually trigger a hot-reload from source files.

### Script Filter Selection Guide

| Situation | Filter | Example |
|---|---|---|
| One script per creature type | `[ScriptFilterCreatureId(id)]` | Unique AI for specific NPC |
| Shared AI for multiple creatures | `[ScriptFilterDefault]` | Generic combat behavior |
| Instance/arena-specific logic | `[ScriptFilterOwnerId(id)]` | Match state handling |
| Zone-wide map scripts | No filter (implement `IMapScript`) | Public event coordination |
| Script owned by entity type T | Implement `IOwnedScript<T>` | Custom turret AI |

### Thread Safety Rules

- **All script methods run on the world tick thread** — do not call blocking I/O, database queries, or long-running loops from any lifecycle hook.
- If you need async operations (e.g., HTTP calls to external services), use `.ConfigureAwait(false)` and never await in synchronous `Update()` callbacks. Prefer `IScriptEventManager` for deferred execution.
- ScriptManager handles reloads atomically: existing collections are preserved during the unload/reload cycle, then re-initialized with fresh script instances.

### Instance Script Patterns

Instance scripts (`Script.Instance`) coordinate with the matching system:
- Override `PublicEventId` property to register with the correct public event instance
- Use base classes: `EventBaseContentMapScript` for PvE encounters, `EventBasePvpContentMapScript` for PvP arenas
- Handle player removal during matches via `OnRemoveFromMap` — transfer control of owned entities (e.g., turrets) to other players before the match ends
- Teleport logic on encounter completion goes in `OnPvpMatchFinish` or equivalent override

## Template Interface Hierarchy

All template interfaces use **default implementations** (`{ }`) — scripts only override what they need:

```
IScript                          ← Base lifecycle: OnLoad(), OnUnload()
├── IGridEntityScript           ← Tick + grid events (OnAddToMap/OnRemoveFromMap, OnEnterRange/OnExitRange)
│   ├── IWorldEntityScript      ← Position commands, zone enter, activation
│   │   └── INonPlayerScript    ← Marker interface for non-player entities
│   │       └── IUnitScript     ← Threat list changes, death/respawn hooks
│   │           └── IPlayerScript  ← Login/logout lifecycle
├── IMapScript                  ← Tick + entity add/remove to map, public event finish
│   └── IInstancedMapScript    ← Marker: instanced content maps (dungeons, arenas)
├── IPublicEventScript          ← Tick + status/phase/objective changes, entity add/remove, match state, votes
├── IQuestScript                ← Tick + quest state change, objective updates
├── ISpellScript                ← Empty marker interface (future use)
├── IContentMapScript           ← Marker for instanced map scripts (PvE encounters)
└── IOwnedScript<T>             ← Owned by specific entity type T (e.g., AI turret behavior)
```

**Invocation Pattern**: Game code calls `scriptCollection.Invoke<T>(action)` where `T` is the interface type. The collection iterates all loaded script instances and casts to `T` — only scripts implementing that interface receive the callback. For example, when a creature dies, the entity system calls `Invoke<IUnitScript>(s => s.OnDeath())`.

## Configuration Model

```csharp
class ScriptConfig {
    bool Enable = false;          // Master on/off switch for all scripting
    string Directory;             // Path to pre-compiled DLL directory
    ScriptDynamicConfig Dynamic;  // Hot-reload settings (see below)
}
class ScriptDynamicConfig {
    bool Enable = false;          // Enable dynamic source compilation and hot-reload
    string Directory;             // Path to .cs source files watched by FileSystemWatcher
}
```

## WorldServer Integration

- **Startup**: `ScriptManager.Initialise()` called at initialization step 3 (after Config/RBAC/DisableManager, before GameTables). Discovers pre-compiled DLLs first, then handles remaining source files.
- **World Tick**: `scriptManager.Update(lastTick)` in the world update callback chain — processes pending reloads and ticks all scripts that implement tick methods.
- **Shutdown**: `ScriptManager.Unload()` called during WorldServer dispose — calls OnUnload on every script instance, then runs GC.WaitForPendingFinalizers loop to ensure assemblies are collected.
- **Commands** (`ScriptCommandCategory`): Two commands under `[Permission.Script]`:
  - `/script reload <assembly> [Assembly|Source]` — triggers hot-reload of a specific assembly (measures and reports elapsed milliseconds)
  - `/script info` — prints full script manager state: assembly count, per-assembly loaded types/instances, filter stats

## Verification

- `dotnet build Source/NexusForever.slnx` — compiles all scripts as part of the full solution build. Script projects are build dependencies of WorldServer, so compilation errors here will break the entire server startup.
- Dynamic source mode: verify hot-reload by editing a `.cs` file in the dynamic directory and observing `/script info` output for updated instance counts after the next tick.
- Instance content requires testing with multiple players to verify encounter synchronization (match start, member join/leave, teleport on completion).

## Child DOX Index

None. Each script project is a sibling within this domain with similar structure.
