# Network Domain

Covers all network protocol layers: packet infrastructure, session management, and per-protocol message handling (Auth, STS, World, Internal).

## Purpose

Define rules for `NexusForever.Network` (core packet/session infrastructure) and its protocol-specific projects (`Network.Auth`, `Network.Sts`, `Network.World`, `Network.Internal`). The domain implements a bit-level binary protocol with keyed DI-driven message dispatch. Total codebase: ~480 source files, 23,000+ lines across all layers.

## Ownership

- **Network**: Core packet reading/writing (`GamePacketReader`/`GamePacketWriter`), fragmented buffer reassembly, session management (`GameSession`, `NetworkManager<T>`), message dispatch via `MessageManager`. ~35 files, 2,300+ lines.
- **Network.Auth**: Authentication protocol messages — login handshake (`ClientHelloAuth → ServerAuthEncrypted → ServerAuthAccepted/Denied + ServerRealmInfo`), realm selection validation. 8 files, ~400 LOC.
- **Network.Sts**: STS ticket protocol with custom `IReadable`/`IWritable` interfaces and local `MessageAttribute`. Handles login flow (`ClientLoginStart → ClientKeyData → ClientLoginFinish`) and game token issuance. 18 files, ~500 LOC.
- **Network.World**: World/game protocol messages — the largest layer at ~300 source files, 15,000+ LOC. Organized into feature areas: entity updates (~15), combat/spells (~12), inventory (~12), housing (~15), group/PvP/public events (~40+), matching (~30), story/cinematic (~15), path system (~30). 880-entry `GameMessageOpcode` enum shared across all layers.
- **Network.Internal**: Inter-service messaging via Rebus (RabbitMQ or Azure Service Bus). Catalogs: Chat (7 types + 30+ format models), Friendship (19 types), Group (18 types), Player (2+ types), Who (1 type), Server availability broadcasts, Match coordination.

## Local Contracts

- **Message classes**: Implement `IReadable.Read(GamePacketReader)` for deserialization and optionally `IWritable.Write(GamePacketWriter)` for serialization back to client.
- **[Message] attribute**: Associates a message class with a `GameMessageOpcode` value. Used by `AddNetworkMessage()` / `AddNetworkAuthMessage()` / `AddNetworkWorldMessage()` during DI registration — these scan assemblies via `TypeWalker.Walk<MessageAttribute>()`.
- **Packet reading order matters**: `GamePacketReader` is stream-based; fields must be read in the exact order they appear on wire. Bit-level reads (`ReadBit()`, `ReadBits(n)`) accumulate internally and flush to byte boundary when needed.
- **FlushBits required**: Every `GamePacketWriter` call sequence that writes variable-length data MUST end with `FlushBits()` or data is silently lost.
- **Both reader/writer are IDisposable**: Always wrap in `using` statements to avoid stream resource leaks.

## Work Guidance

### Adding a New Network.World Message

1. Add new entry to `GameMessageOpcode` enum in the appropriate range (Client* = client→server, Server* = server→client)
2. Create message class under `Network.World/Message/Model/<FeatureArea>/` with `[Message(GameMessageOpcode.Value)]` attribute
3. Implement `IReadable.Read()` — fields must be deserialized in wire order; use `GamePacketReader` typed methods (`ReadUInt()`, `ReadPackedFloat()`, `ReadVector3()`)
4. If the server needs to send a response back, implement `IWritable.Write()` on the same class or create a separate response model
5. Register with DI: call `AddNetworkWorldMessage()` in `NexusForever.WorldServer/ServiceCollectionExtensions.cs` (scans assembly for `[Message]` classes)
6. Create handler: `IMessageHandler<IGameSession, YourMessage>` in `WorldServer/Network/Message/Handler/<FeatureArea>/`, implement `HandleMessage(session, message)`

### PacketReader/Writer Gotchas

- **Never mutate a packet mid-read**: Always copy the underlying byte array before modifying `GamePacketReader` state. The reader maintains internal bit-position tracking that breaks on mutation.
- **PackedFloat encoding**: Uses 16-bit half-float (IEEE 754 compressed). Precision loss is intentional for network efficiency — suitable for position/rotation but not precise game calculations. Always read as `float`, never compare packed floats with equality.
- **Bit-packing safety**: When writing, accumulate bits in the writer's internal buffer and call `FlushBits()` before sending. The packet is incomplete until flushed.

### Network World Protocol Scale

The World protocol layer handles ~20Hz tick traffic for entity updates (health, stats, threat lists). Keep new message serialization minimal:
- Avoid allocations inside `Read()`/`Write()` methods — the hot path runs every tick on world update threads
- Use value types where possible; avoid `new` allocations in packet handlers
- Telegraph positions and spell targets use shared `TelegraphPosition` struct to reduce per-message size

### Thread Safety

- Packets are queued via `ConcurrentQueue<ClientGamePacket>` / `ConcurrentQueue<ServerGamePacket>` — safe for concurrent enqueue/dequeue from network I/O threads and world tick thread
- Message handlers may run from either the network I/O thread or the world update thread depending on when they're invoked during session processing. **Never assume single-threaded access to entities** in a handler; synchronize shared state explicitly.
- `NetworkManager<T>` defers all add/remove/update operations through queues — session ID changes are applied during Update(), never mid-tick.

### Inter-Service Messaging (Network.Internal)

- All inter-service communication uses Rebus over RabbitMQ or Azure Service Bus, configured per-server in their config files (`BrokerProvider` enum: `RabbitMq` or `AzureServiceBus`)
- Messages are typed POCOs — no serialization attributes needed; Rebus handles JSON serialization via `System.Text.Json`
- **Idempotency required**: The broker may deliver duplicate messages during retries. Handlers must be safe to call multiple times with the same payload (e.g., Friendship invite handlers check for existing invites before creating).
- **Shared message types** (`Identity`, `IdentityName`, `Position`) are used across all inter-service messages — never change their structure without considering cross-domain impact.

### Encryption Flow

1. Client connects to AuthServer → `ClientHelloAuth` exchange establishes symmetric session key via `PacketCrypt` (AES-based)
2. AuthServer validates credentials and issues realm info (`ServerRealmInfo`)
3. Client selects realm, connects to WorldServer on port 24000 with encrypted payload using the same session key
4. All subsequent packets are wrapped: header = 4-byte size + 2-byte opcode + N bytes encrypted payload (or unencrypted for non-sensitive messages)
5. `GameSession.FlushPackets()` writes raw bytes via `SendRaw()` — encryption is handled transparently in the write path

## Verification

- `dotnet build Source/NexusForever.slnx` — compiles all network layers with dependencies
- Packet format changes require cross-checking against WildStar client protocol expectations (opcode values, field ordering, packed types)
- Internal broker: verify Rebus configuration matches deployed message queue (RabbitMQ vs Azure Service Bus) in server config files
- Message handler registration verified by checking `dotnet build` output for missing `[Message]` attribute registrations during DI setup

## Child DOX Index

None. Each Network.* project is a sibling at the same complexity level within this domain.
