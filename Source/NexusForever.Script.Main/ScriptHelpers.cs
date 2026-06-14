using Microsoft.Extensions.DependencyInjection;
using NexusForever.Script.Main.Tutorial;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.GameTable;
using NexusForever.Game.Map;
using NexusForever.Shared;
using System.Numerics;

// ReSharper disable MemberCanBePrivate.Global

namespace NexusForever.Script.Main
{
    /// <summary>
    /// for testing
    /// </summary>
    public static class ScriptHelpers
    {
        private const uint InvisibleCreatureId = 16588; // Invisible Unit for Field Spell Visuals (1.2 hit radius)
        private const uint InvisibleDisplayId = 24307;
        
        public static Spell4Entry GetSpellEntry(uint spellId) => GameTableManager.Instance.Spell4.GetEntry(spellId);
        public static Spell4Entry GetSpellEntry(this QuestSpells spell) => GetSpellEntry((uint) spell); 
        
        public static uint SpellCastTime(this QuestSpells spell) => spell.GetSpellEntry().CastTime;
        public static uint SpellCooldown(this QuestSpells spell) => spell.GetSpellEntry().SpellCoolDown;
        
        public static (float, float) GetSpellRange(QuestSpells spell)
        {
            Spell4Entry spellEntry = spell.GetSpellEntry();
            return (spellEntry.TargetMinRange, spellEntry.TargetMaxRange);
        }

        public static Spell4Entry[] GetEntitySpells(this ICreatureEntity creatureEntity)
        {
            List<Spell4Entry> entitySpells = [];
            entitySpells.AddRange(creatureEntity.CreatureSpells.Select(creatureSpellEntry => GameTableManager.Instance.Spell4.GetEntry(creatureSpellEntry.Spell4Id)));
            return entitySpells.ToArray();
        }
        
        /// <summary>
        /// This does not change collision or hit?
        /// </summary>
        /// <param name="entity"></param>
        public static void SetEntityInvisible(this ICreatureEntity entity)
        {
            entity.CreatureId = InvisibleCreatureId;
            entity.DisplayInfo = InvisibleDisplayId;
        }

        public static ICreatureEntity CreateInvisibleEntity(IBaseMap map, Vector3 spawnLocation) => CreateEntity(map, InvisibleCreatureId, InvisibleDisplayId, spawnLocation, 0);

        public static ICreatureEntity Clone(this ICreatureEntity entity, Vector3 location) => CreateEntityClone(entity.Map, entity, location);

        /// <summary>
        /// Returns entity that will not have valid guid until next update
        /// can use existingEntity.Map.ForceAddImmediate()
        /// </summary>
        /// <param name="map"></param>
        /// <param name="existingEntity"></param>
        /// <param name="spawnLocation"></param>
        /// <returns></returns>
        public static ICreatureEntity CreateEntityClone(IBaseMap map, IWorldEntity existingEntity, Vector3 spawnLocation) => CreateEntity(map, existingEntity.CreatureId, existingEntity.DisplayInfo, spawnLocation);
        
        public static ICreatureEntity CreateEntity(IBaseMap map, uint creatureId, uint displayId, Vector3 spawnLocation, float scale = -1)
        {
            INonPlayerEntity newEntity = LegacyServiceProvider.Provider.GetRequiredService<INonPlayerEntity>();
            newEntity.Initialise(creatureId);
            newEntity.DisplayInfo = displayId;

            if (scale >= 0)
            {
                newEntity.MovementManager.SetScale(scale);
            }
            
            MapPosition mapPosition = new MapPosition { Position = spawnLocation };
            if (map.CanEnter(newEntity, mapPosition))
            {
                map.EnqueueAdd(newEntity, mapPosition);
            } 
            
            return newEntity;
        }
        
        /// <summary>
        /// this was for duplicating
        /// </summary>
        /// <param name="cloneEntity"></param>
        /// <param name="existingEntity"></param>
        public static void CloneEntityConfiguration(IWorldEntity existingEntity, ICreatureEntity cloneEntity)
        {
            //cloneEntity.MovementManager.SetRotation(existingEntity.MovementManager.GetRotation(), false);
            cloneEntity.MovementManager.SetScale(existingEntity.MovementManager.GetScale());
            cloneEntity.SetFaction(existingEntity.Faction2); // do not set temp faction first, both = 0 and will throw error
            cloneEntity.DisplayInfo = existingEntity.DisplayInfo; //entity is invisible w/o display info set
            cloneEntity.StandState = StandState.Stand; // owner.StandState;
            
            cloneEntity.ModifyHealth(cloneEntity.MaxHealth, DamageType.Heal, cloneEntity); // max health already set
        }
    }
}
