using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusForever.Database;
using NexusForever.Database.Auth;
using NexusForever.Database.Character;
using NexusForever.Game;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.Map.Instance;
using NexusForever.Game.Entity;
using NexusForever.Game.Static;
using NexusForever.Game.Static.RBAC;
using NexusForever.GameTable;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Shared;

namespace NexusForever.WorldServer.Web.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        // ---- MAPS / ENTITIES ----

        /// <summary>
        /// List loaded maps with player and entity counts.
        /// </summary>
        [HttpGet("loaded-maps")]
        public IActionResult GetLoadedMaps()
        {
            var result = new List<object>();
            foreach (var map in Game.Map.MapManager.Instance.LoadedMaps)
            {
                try
                {
                    if (map is IBaseMap baseMap)
                    {
                        var entities = baseMap.GetAllEntities().ToList();
                        result.Add(new
                        {
                            worldId = map.Entry.Id,
                            name = map.Entry.LocalizedTextIdName != 0u
                                ? GameTableManager.Instance.GetTextTable(Game.Static.Language.English)?.GetEntry(map.Entry.LocalizedTextIdName) ?? $"World {map.Entry.Id}"
                                : $"World {map.Entry.Id}",
                            typeName = map.Entry.Type.ToString(),
                            playerCount = entities.OfType<IPlayer>().Count(),
                            entityCount = entities.Count,
                            isInstanced = false
                        });
                    }
                    else if (map is IInstancedMap)
                    {
                        result.Add(new
                        {
                            worldId = map.Entry.Id,
                            name = map.Entry.LocalizedTextIdName != 0u
                                ? GameTableManager.Instance.GetTextTable(Game.Static.Language.English)?.GetEntry(map.Entry.LocalizedTextIdName) ?? $"World {map.Entry.Id}"
                                : $"World {map.Entry.Id}",
                            typeName = map.Entry.Type.ToString(),
                            playerCount = 0,
                            entityCount = 0,
                            isInstanced = true
                        });
                    }
                }
                catch { /* map may not be fully initialized */ }
            }
            return Ok(result);
        }

        /// <summary>
        /// List all loaded instances with player/entity counts and state.
        /// </summary>
        [HttpGet("instances")]
        public IActionResult GetInstances()
        {
            var result = new List<object>();
            foreach (var map in Game.Map.MapManager.Instance.LoadedMaps)
            {
                try
                {
                    if (map is IBaseMap baseMap && !(map is IInstancedMap))
                    {
                        var entities = baseMap.GetAllEntities().ToList();
                        result.Add(new
                        {
                            worldId = map.Entry.Id,
                            name = map.Entry.LocalizedTextIdName != 0u
                                ? GameTableManager.Instance.GetTextTable(Game.Static.Language.English)?.GetEntry(map.Entry.LocalizedTextIdName) ?? $"World {map.Entry.Id}"
                                : $"World {map.Entry.Id}",
                            typeName = map.Entry.Type.ToString(),
                            instanceId = null as string,
                            state = "Active",
                            playerCount = entities.OfType<IPlayer>().Count(),
                            entityCount = entities.Count
                        });
                    }
                    else if (map is IInstancedMap instanced)
                    {
                        foreach (var instance in instanced.AllInstances)
                        {
                            var entities = instance.GetAllEntities().ToList();
                            string state = "Active";
                            if (instance.UnloadStatus.HasValue)
                                state = instance.UnloadStatus.Value.ToString();

                            result.Add(new
                            {
                                worldId = map.Entry.Id,
                                name = map.Entry.LocalizedTextIdName != 0u
                                    ? GameTableManager.Instance.GetTextTable(Game.Static.Language.English)?.GetEntry(map.Entry.LocalizedTextIdName) ?? $"World {map.Entry.Id}"
                                    : $"World {map.Entry.Id}",
                                typeName = map.Entry.Type.ToString(),
                                instanceId = instance.InstanceId.ToString(),
                                state,
                                playerCount = entities.OfType<IPlayer>().Count(),
                                entityCount = entities.Count
                            });
                        }
                    }
                }
                catch { /* map/instance may not be fully initialized */ }
            }
            return Ok(result);
        }

        /// <summary>
        /// Unload a stuck or unwanted map instance.
        /// </summary>
        [HttpDelete("instances/{instanceId}/unload")]
        public IActionResult UnloadInstance(string instanceId)
        {
            foreach (var map in Game.Map.MapManager.Instance.LoadedMaps)
            {
                if (map is not IInstancedMap instanced)
                    continue;

                var target = instanced.AllInstances.FirstOrDefault(i => i.InstanceId == new Guid(instanceId));
                if (target != null)
                {
                    target.Unload();
                    return Ok(new { success = true });
                }
            }
            return NotFound($"Map instance {instanceId} not found");
        }

        /// <summary>
        /// List all worlds with player and entity counts (from game table, not just loaded maps).
        /// </summary>
        [HttpGet("maps")]
        public IActionResult GetMaps()
        {
            var loadedMaps = Game.Map.MapManager.Instance.LoadedMaps
                .OfType<IBaseMap>()
                .ToDictionary(m => m.Entry.Id);

            var result = new List<object>();
            foreach (var entry in GameTableManager.Instance.World.Entries)
            {
                int playerCount = 0;
                int entityCount = 0;
                if (loadedMaps.TryGetValue(entry.Id, out var loadedMap))
                {
                    try
                    {
                        var entities = loadedMap.GetAllEntities().ToList();
                        entityCount = entities.Count;
                        playerCount = entities.OfType<IPlayer>().Count();
                    }
                    catch { /* map may not be fully initialized */ }
                }

                var textEntry = GameTableManager.Instance.GetTextTable(Game.Static.Language.English)?.GetEntry(entry.LocalizedTextIdName);
                result.Add(new
                {
                    worldId = entry.Id,
                    name = textEntry ?? $"World {entry.Id}",
                    typeName = entry.Type.ToString(),
                    playerCount,
                    entityCount,
                    isLoaded = loadedMap != null
                });
            }
            return Ok(result);
        }

        /// <summary>
        /// List all entities on a map, filtered by type.
        /// </summary>
        [HttpGet("entities/{worldId}")]
        public IActionResult GetEntities(ushort worldId, string type = null)
        {
            var maps = Game.Map.MapManager.Instance.LoadedMaps
                .Where(m => m.Entry.Id == worldId).ToList();

            if (!maps.Any())
                return NotFound($"No loaded maps for world {worldId}");

            var entities = new List<object>();
            foreach (var map in maps)
            {
                if (map is IBaseMap baseMap)
                {
                    CollectEntityData(baseMap, type, entities);
                }
                else if (map is IInstancedMap instanced)
                {
                    foreach (var instance in instanced.AllInstances)
                        CollectEntityData(instance, type, entities);
                }
            }

            return Ok(entities);
        }

        /// <summary>
        /// Get detailed state for a specific entity by GUID.
        /// </summary>
        [HttpGet("entities/detail/{guid}")]
        public IActionResult GetEntityDetail(uint guid)
        {
            IWorldEntity foundEntity = null;

            foreach (var map in Game.Map.MapManager.Instance.LoadedMaps)
            {
                if (map is not IBaseMap baseMap) continue;

                var entity = baseMap.GetEntity<IWorldEntity>(guid);
                if (entity != null)
                {
                    foundEntity = entity;
                    break;
                }
            }

            if (foundEntity == null)
                return NotFound($"Entity with GUID {guid} not found on any loaded map");

            var detail = new Dictionary<string, object>
            {
                ["guid"] = foundEntity.Guid,
                ["entityId"] = foundEntity.EntityId,
                ["type"] = foundEntity.Type.ToString(),
                ["creatureId"] = foundEntity.CreatureId,
                ["position"] = new { x = foundEntity.Position.X, y = foundEntity.Position.Y, z = foundEntity.Position.Z },
                ["rotation"] = new { x = foundEntity.Rotation.X, y = foundEntity.Rotation.Y, z = foundEntity.Rotation.Z },
                ["health"] = foundEntity.Health,
                ["maxHealth"] = foundEntity.MaxHealth,
                ["shield"] = foundEntity.Shield,
                ["maxShieldCapacity"] = foundEntity.MaxShieldCapacity,
                ["level"] = foundEntity.Level,
                ["faction1"] = foundEntity.Faction1,
                ["faction2"] = foundEntity.Faction2
            };

            // Type-specific fields for players
            if (foundEntity is IPlayer player)
            {
                detail["name"] = player.Name;
                detail["race"] = player.Race;
                detail["characterClass"] = player.Class;
                detail["accountId"] = player.Account.Id;
                detail["level"] = player.Level;
                detail["path"] = player.Path.ToString();
            }

            // Type-specific fields for NPCs/creatures
            if (foundEntity is INonPlayerEntity npc)
            {
                var nameBuilder = new System.Text.StringBuilder();
                NexusForever.WorldServer.Command.Shared.EntityUtility.BuildHeader(nameBuilder, foundEntity, Game.Static.Language.English);
                detail["name"] = GameTableManager.Instance.GetTextTable(Game.Static.Language.English).GetEntry(foundEntity.CreatureId) ?? "Unknown";

                var creature2Entry = GameTableManager.Instance.Creature2.GetEntry(foundEntity.CreatureId);
                if (creature2Entry != null)
                {
                    detail["creatureInfo"] = new
                    {
                        levelMin = creature2Entry.MinLevel,
                        levelMax = creature2Entry.MaxLevel,
                        difficulty = creature2Entry.Creature2DifficultyId,
                        family = creature2Entry.Creature2FamilyId
                    };
                }

                // Vendor info if applicable
                try
                {
                    var vendorInfo = npc.VendorInfo;
                    detail["isVendor"] = true;
                    detail["vendorInfo"] = new { buyMultiplier = vendorInfo.BuyPriceMultiplier, sellMultiplier = vendorInfo.SellPriceMultiplier };
                }
                catch { /* no vendor info */ }

                // Respawn state for NPCs
                try
                {
                    detail["isDead"] = !npc.IsAlive;
                }
                catch { /* not a unit type */ }
            }

            // Properties (for any IWorldEntity with properties)
            try
            {
                var props = foundEntity.GetProperties().ToList();
                detail["properties"] = props.Select(p => new
                {
                    property = p.Property.ToString(),
                    baseValue = p.BaseValue,
                    value = p.Value
                }).ToList();
            }
            catch { /* entity may not have properties */ }

            // Combat info (for IUnitEntity)
            if (foundEntity is IUnitEntity unit && !unit.IsAlive)
            {
                detail["isDead"] = true;
            }
            else if (foundEntity is IUnitEntity combatUnit)
            {
                try
                {
                    detail["inCombat"] = combatUnit.InCombat;

                    // Threat list if applicable
                    var hostiles = combatUnit.ThreatManager.ToList();
                    detail["threatList"] = hostiles.Select(h => new { unitId = h.HatedUnitId, threat = h.Threat }).ToList();
                }
                catch { /* may not have threat manager */ }

                try
                {
                    detail["isAlive"] = combatUnit.IsAlive;
                }
                catch { /* no IsAlive property */ }
            }

            return Ok(detail);
        }

        /// <summary>
        /// Modify an entity's display info (online only).
        /// </summary>
        [HttpPut("entities/{guid}/display")]
        public IActionResult SetEntityDisplay(uint guid, [FromBody] DisplayInfoRequest request)
        {
            IWorldEntity entity = FindEntity(guid);
            if (entity == null) return NotFound();

            var displayEntry = GameTableManager.Instance.Creature2DisplayInfo.GetEntry(request.DisplayInfoId);
            if (request.DisplayInfoId != 0 && displayEntry == null)
                return BadRequest("Invalid display info ID");

            entity.DisplayInfo = request.DisplayInfoId;
            return Ok(new { success = true });
        }

        /// <summary>
        /// Set an entity's temporary faction (online only).
        /// </summary>
        [HttpPut("entities/{guid}/faction")]
        public IActionResult SetEntityFaction(uint guid, [FromBody] FactionRequest request)
        {
            IWorldEntity entity = FindEntity(guid);
            if (entity == null) return NotFound();

            entity.SetTemporaryFaction((Game.Static.Reputation.Faction)request.FactionId);
            return Ok(new { success = true });
        }

        // ---- SERVER STATUS ----

        /// <summary>
        /// Get server status overview.
        /// </summary>
        [HttpGet("server/status")]
        public IActionResult GetServerStatus()
        {
            int onlinePlayers = 0;
            try { onlinePlayers = PlayerManager.Instance.Count(); } catch { /* player manager may not be initialized */ }

            int loadedMaps = 0;
            try { loadedMaps = Game.Map.MapManager.Instance.LoadedMaps.Count(); } catch { /* map manager may not be initialized */ }

            return Ok(new
            {
                onlinePlayers,
                loadedMaps
            });
        }

        // ---- ACCOUNTS ----

        /// <summary>
        /// List/search accounts with pagination.
        /// </summary>
        [HttpGet("accounts")]
        public async Task<IActionResult> GetAccounts(string search = null, int offset = 0, int limit = 50)
        {
            var (accounts, total) = await DatabaseManager.Instance.GetDatabase<AuthDatabase>().GetAccountsAsync(search, offset, limit);

            return Ok(new
            {
                accounts = accounts.Select(a => new
                {
                    a.Id,
                    Email = a.Email,
                    a.CreateTime,
                    Roles = a.AccountRole.Select(ar => ar.Role.Name).ToList(),
                    IsBanned = a.AccountSuspension.Any(s => s.EndTime == null || s.EndTime > System.DateTime.UtcNow)
                }).ToList(),
                total,
                offset,
                limit
            });
        }

        /// <summary>
        /// Get account detail with full relationships.
        /// </summary>
        [HttpGet("accounts/{id}")]
        public async Task<IActionResult> GetAccount(uint id)
        {
            var account = await DatabaseManager.Instance.GetDatabase<AuthDatabase>().GetAccountByIdAsync(id);
            if (account == null) return NotFound();

            return Ok(new
            {
                Id = account.Id,
                Email = account.Email,
                CreateTime = account.CreateTime,
                Roles = account.AccountRole.Select(ar => new { ar.RoleId, Name = ar.Role.Name }).ToList(),
                Permissions = account.AccountPermission.Select(ap => ap.Permission.Name).ToList(),
                Suspensions = account.AccountSuspension.Select(s => new
                {
                    s.Id,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    IsPermanent = s.EndTime == null,
                    IsActive = s.EndTime == null || s.EndTime > System.DateTime.UtcNow,
                    Reason = s.Reason
                }).ToList()
            });
        }

        /// <summary>
        /// Ban an account.
        /// </summary>
        [HttpPost("accounts/{id}/ban")]
        public async Task<IActionResult> BanAccount(uint id, [FromBody] BanRequest request)
        {
            var account = await DatabaseManager.Instance.GetDatabase<AuthDatabase>().GetAccountByIdAsync(id);
            if (account == null)
                return NotFound($"Account {id} not found");

            System.DateTime? endTime = null;
            if (!string.IsNullOrEmpty(request.Duration))
            {
                if (System.TimeSpan.TryParse(request.Duration, out var parsed))
                    endTime = System.DateTime.UtcNow + parsed;
                else
                    return BadRequest($"Invalid duration format '{request.Duration}'. Use 'd.hh:mm:ss' (e.g. '7.00:00:00' for 7 days)");
            }

            DatabaseManager.Instance.GetDatabase<AuthDatabase>().BanAccount(id, request.Reason ?? "", endTime);

            // Disconnect if online
            try
            {
                var player = PlayerManager.Instance.GetPlayerByAccountId(id);
                player?.Session.ForceDisconnect();
            }
            catch { /* player may not exist */ }

            return Ok(new { success = true });
        }

        /// <summary>
        /// Unban an account (end active bans).
        /// </summary>
        [HttpPost("accounts/{id}/unban")]
        public IActionResult UnbanAccount(uint id)
        {
            DatabaseManager.Instance.GetDatabase<AuthDatabase>().UnbanAccount(id);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Create a new account.
        /// </summary>
        [HttpPost("accounts")]
        public IActionResult CreateAccount([FromBody] CreateAccountRequest request)
        {
            var (salt, verifier) = NexusForever.Cryptography.PasswordProvider.GenerateSaltAndVerifier(request.Email ?? "", request.Password ?? "");
            uint roleId = request.Role.HasValue ? (uint)request.Role.Value : (uint)(Shared.Configuration.SharedConfiguration.Instance.Get<NexusForever.Game.Configuration.Model.RealmConfig>().DefaultRole ?? Role.Player);

            DatabaseManager.Instance.GetDatabase<AuthDatabase>().CreateAccount(request.Email ?? "", salt, verifier, roleId);
            return Ok();
        }

        /// <summary>
        /// Delete an account.
        /// </summary>
        [HttpDelete("accounts/{id}")]
        public IActionResult DeleteAccount(uint id)
        {
            if (DatabaseManager.Instance.GetDatabase<AuthDatabase>().DeleteAccountById(id))
                return Ok();
            return NotFound();
        }

        /// <summary>
        /// Set/change primary role for an account.
        /// </summary>
        [HttpPut("accounts/{id}/role")]
        public IActionResult SetAccountRole(uint id, [FromBody] RoleRequest request)
        {
            DatabaseManager.Instance.GetDatabase<AuthDatabase>().SetPrimaryRole(id, request.RoleId);
            return Ok(new { success = true });
        }

        /// <summary>
        /// List characters for an account.
        /// </summary>
        [HttpGet("accounts/{id}/characters")]
        public async Task<IActionResult> GetAccountCharacters(uint id, int offset = 0, int limit = 50)
        {
            var chars = await DatabaseManager.Instance.GetDatabase<CharacterDatabase>().GetCharacters(id);

            return Ok(new
            {
                characters = chars.Skip(offset).Take(limit).Select(c => new
                {
                    c.Id,
                    Name = c.Name,
                    Level = c.Level,
                    Race = c.Race,
                    CharacterClass = c.Class,
                    IsOnline = c.IsOnline,
                    LastOnline = c.LastOnline,
                    WorldId = c.WorldId,
                    CreateTime = c.CreateTime
                }).ToList(),
                total = chars.Count,
                offset,
                limit
            });
        }

        // ---- CHARACTERS ----

        /// <summary>
        /// List/search characters with pagination.
        /// </summary>
        [HttpGet("characters")]
        public IActionResult GetCharacters(string name = null, uint? accountId = null, bool? online = null, int offset = 0, int limit = 50)
        {
            var (characters, total) = DatabaseManager.Instance.GetDatabase<CharacterDatabase>().GetCharacters(name, accountId, online, offset, limit);

            return Ok(new
            {
                characters = characters.Select(c => new
                {
                    c.Id,
                    Name = c.Name,
                    AccountId = c.AccountId,
                    Level = c.Level,
                    Race = c.Race,
                    CharacterClass = c.Class,
                    FactionId = c.FactionId,
                    IsOnline = c.IsOnline && PlayerManager.Instance.GetPlayer(c.Id) != null,
                    LastOnline = c.LastOnline,
                    WorldId = c.WorldId,
                    WorldZoneId = c.WorldZoneId
                }).ToList(),
                total,
                offset,
                limit
            });
        }

        /// <summary>
        /// Get character detail with items, quests, spells.
        /// </summary>
        [HttpGet("characters/{id}")]
        public async Task<IActionResult> GetCharacter(ulong id)
        {
            var character = await DatabaseManager.Instance.GetDatabase<CharacterDatabase>().GetCharacterDetailAsync(id);
            if (character == null) return NotFound();

            return Ok(new
            {
                Id = character.Id,
                Name = character.Name,
                AccountId = character.AccountId,
                Level = character.Level,
                Race = character.Race,
                CharacterClass = character.Class,
                Sex = character.Sex,
                FactionId = character.FactionId,
                IsOnline = character.IsOnline,
                LastOnline = character.LastOnline,
                WorldId = character.WorldId,
                WorldZoneId = character.WorldZoneId,
                Position = new { x = character.LocationX, y = character.LocationY, z = character.LocationZ },
                TotalXp = character.TotalXp,
                TimePlayedTotal = character.TimePlayedTotal,
                ActivePath = character.ActivePath,
                Title = character.Title,
                Items = character.Item.Select(i => new
                {
                    i.Id,
                    ItemId = i.ItemId,
                    StackCount = i.StackCount,
                    Charges = i.Charges,
                    Location = i.Location
                }).ToList(),
                Quests = character.Quest.Select(q => new
                {
                    q.QuestId,
                    State = q.State,
                    Flags = q.Flags,
                    Objectives = q.QuestObjective.Select(o => new { o.Index, o.Progress }).ToList()
                }).ToList(),
                Spells = character.Spell.Select(s => new
                {
                    s.Spell4BaseId,
                    Tier = s.Tier
                }).ToList(),
                Stats = character.Stat.Select(s => new
                {
                    Stat = s.Stat,
                    Value = s.Value
                }).ToList()
            });
        }

        /// <summary>
        /// Add item to an online character.
        /// </summary>
        [HttpPost("characters/{id}/item")]
        public IActionResult AddItem(ulong id, [FromBody] ItemRequest request)
        {
            var player = PlayerManager.Instance.GetPlayer(id);
            if (player == null) return BadRequest("Character is offline");

            if (NexusForever.Game.ItemManager.Instance.GetItemInfo(request.ItemId) == null)
                return BadRequest($"Item ID {request.ItemId} does not exist");

            try
            {
                player.Inventory.ItemCreate(Game.Static.Entity.InventoryLocation.Inventory, request.ItemId, request.Quantity.GetValueOrDefault(1u), NexusForever.Network.World.Message.Static.ItemUpdateReason.Cheat, request.Charges.GetValueOrDefault(1u));
                return Ok(new { success = true });
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Teleport an online character.
        /// </summary>
        [HttpPost("characters/{id}/teleport")]
        public IActionResult TeleportCharacter(ulong id, [FromBody] TeleportRequest request)
        {
            var player = PlayerManager.Instance.GetPlayer(id);
            if (player == null) return BadRequest("Character is offline");

            var worldEntry = GameTableManager.Instance.World.GetEntry(request.WorldId);
            if (worldEntry == null)
                return BadRequest($"World ID {request.WorldId} is not a valid world");

            if (DisableManager.Instance.IsDisabled(DisableType.World, request.WorldId))
                return BadRequest($"World {request.WorldId} is disabled");

            try
            {
                player.TeleportTo(request.WorldId, request.X, request.Y, request.Z);
                return Ok(new { success = true });
            }
            catch (System.Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Set character level via database for offline characters.
        /// </summary>
        [HttpPut("characters/{id}/level")]
        public IActionResult SetCharacterLevel(ulong id, [FromBody] LevelRequest request)
        {
            if (request.Level < 1 || request.Level > 50)
                return BadRequest("Level must be between 1 and 50");

            var player = PlayerManager.Instance.GetPlayer(id);
            if (player != null)
            {
                player.Level = request.Level;
                return Ok(new { success = true });
            }

            if (!DatabaseManager.Instance.GetDatabase<CharacterDatabase>().SetCharacterLevel(id, request.Level))
                return NotFound($"Character {id} not found");

            return Ok(new { success = true });
        }

        // ---- ONLINE PLAYERS ----

        /// <summary>
        /// List all online players with current state.
        /// </summary>
        [HttpGet("online")]
        public IActionResult GetOnlinePlayers()
        {
            try
            {
                var players = PlayerManager.Instance.ToList();
                return Ok(players.Select(p => new
                {
                    characterId = p.CharacterId,
                    identity = new { Id = p.Identity.Id, RealmId = p.Identity.RealmId },
                    name = p.Name,
                    accountId = p.Account.Id,
                    level = p.Level,
                    race = p.Race,
                    characterClass = p.Class,
                    path = p.Path.ToString(),
                    sex = p.Sex,
                    factionId = (uint)p.Faction1,
                    worldId = 0, // map.entry may not be accessible here safely
                    position = new { x = p.Position.X, y = p.Position.Y, z = p.Position.Z }
                }).ToList());
            }
            catch
            {
                return Ok(new List<object>());
            }
        }

        // ---- BROADCAST ----

        /// <summary>
        /// Send a server-wide broadcast message to all online players.
        /// </summary>
        [HttpPost("server/broadcast")]
        public IActionResult Broadcast([FromBody] BroadcastRequest request)
        {
            var broadcast = new ServerRealmBroadcast
            {
                Tier = (BroadcastTier)request.Tier,
                Message = request.Message
            };

            foreach (var player in PlayerManager.Instance)
                player.Session.EnqueueMessageEncrypted(broadcast);

            return Ok(new { success = true });
        }

        // ---- HELPER METHODS ----

        private IWorldEntity FindEntity(uint guid)
        {
            foreach (var map in Game.Map.MapManager.Instance.LoadedMaps)
            {
                if (map is IBaseMap baseMap)
                {
                    var entity = baseMap.GetEntity<IWorldEntity>(guid);
                    if (entity != null) return entity;
                }
            }
            return null;
        }

        private void CollectEntityData(IBaseMap baseMap, string type, List<object> entities)
        {
            IEnumerable<IGridEntity> allEntities;
            if (type == null || type.Equals("all", System.StringComparison.OrdinalIgnoreCase))
                allEntities = baseMap.GetAllEntities();
            else
            {
                var entityType = (Game.Static.Entity.EntityType)System.Enum.Parse(typeof(Game.Static.Entity.EntityType), type, true);
                allEntities = baseMap.GetAllEntities().OfType<IWorldEntity>().Where(e => e.Type == entityType);
            }

            foreach (var entity in allEntities)
            {
                if (entity is not IWorldEntity worldEntity) continue;

                string name = "Unknown";
                if (worldEntity is IPlayer player)
                    name = player.Name;
                else
                {
                    var creatureEntry = GameTableManager.Instance.Creature2.GetEntry(worldEntity.CreatureId);
                    if (creatureEntry != null)
                    {
                        var textTable = GameTableManager.Instance.GetTextTable(Game.Static.Language.English);
                        name = textTable?.GetEntry(creatureEntry.LocalizedTextIdName) ?? "Unknown";
                    }
                }

                entities.Add(new
                {
                    guid = worldEntity.Guid,
                    entityId = worldEntity.EntityId,
                    type = worldEntity.Type.ToString(),
                    creatureId = worldEntity.CreatureId,
                    name,
                    position = new { x = worldEntity.Position.X, y = worldEntity.Position.Y, z = worldEntity.Position.Z },
                    healthPct = (worldEntity.MaxHealth > 0) ? System.Math.Round(worldEntity.Health / worldEntity.MaxHealth * 100.0) : 100
                });
            }
        }

        // ---- REQUEST DTOs ----

        public class DisplayInfoRequest
        {
            public uint DisplayInfoId { get; set; }
        }

        public class FactionRequest
        {
            public ushort FactionId { get; set; }
        }

        public class BanRequest
        {
            public string Reason { get; set; } = "";
            public string Duration { get; set; }
        }

        public class CreateAccountRequest
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public Role? Role { get; set; }
        }

        public class RoleRequest
        {
            public uint RoleId { get; set; }
        }

        public class ItemRequest
        {
            public uint ItemId { get; set; }
            public uint? Quantity { get; set; }
            public uint? Charges { get; set; }
        }

        public class TeleportRequest
        {
            public ushort WorldId { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
        }

        public class LevelRequest
        {
            public byte Level { get; set; } = 1;
        }

        public class BroadcastRequest
        {
            public int Tier { get; set; }
            public string Message { get; set; } = "";
        }
    }
}
