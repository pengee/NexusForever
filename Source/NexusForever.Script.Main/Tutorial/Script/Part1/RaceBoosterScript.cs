using System.Collections.Concurrent;
using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;
using NexusForever.Shared;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    /// <summary>
    /// This grants speed bonus and extra gas on extra boost (arrow) entities
    ///  and speed bonus on holoring entities
    /// </summary>
    [ScriptFilterCreatureId((uint) ObjectiveEntities.ExtraBooster, (uint) ObjectiveEntities.Holoring)]
    public class RaceBoosterScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private IWorldEntity owner;
        
        private const float CastCooldownSeconds = 1f;
        private const float RangeCheckRadius = 6;
        private const byte CheckCooldown = 200;
        
        private double lastUpdate;
        
        private float GetRangeCheckRadius() => owner.CreatureId == (uint) ObjectiveEntities.ExtraBooster ? RangeCheckRadius : RangeCheckRadius * 1.5f;
        
        private readonly HashSet<IPlayer> activePlayers = [];
        
        private static double lastTriggerTime;

        #region Dependency Injection
        private readonly IFactory<ISpellParameters> spellParameterFactory;

        public RaceBoosterScript(IFactory<ISpellParameters> spellParameterFactory)
        {
            this.spellParameterFactory = spellParameterFactory;
        }
        #endregion

        public void OnLoad(IWorldEntity entityOwner)
        {
            owner = entityOwner;
            entityOwner.SetInRangeCheck(GetRangeCheckRadius());
        }

        public void OnRemoveFromMap(IBaseMap map) => activePlayers.Clear();

        public void Update(double lastTick)
        {
            double currentTime = TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalMilliseconds;
            
            if(currentTime - lastUpdate < CheckCooldown) { return; }
            lastUpdate = currentTime;
            
            foreach (IPlayer player in activePlayers)
            {
                // Check Distance
                float playerDistance = Vector3.Distance(player.Position, owner.Position);
                if (playerDistance > GetRangeCheckRadius()) { continue; }
                
                if (IsOnCooldown()) { continue; } //this should only trigger once, not per player
                
                GrantPlayerSpellEffect(player);
            }
        }

        private static bool IsOnCooldown()
        {
            double currentTime = TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalSeconds;
            double elapsedTime = currentTime - lastTriggerTime;
            if (elapsedTime < CastCooldownSeconds) { return true; }
            lastTriggerTime = currentTime;
            return false;
        }

        public void OnEnterRange(IGridEntity entity)
        {
            if (entity is not IPlayer player) { return; }
            activePlayers.Add(player);
        }

        public void OnExitRange(IGridEntity entity)
        {
            if (entity is not IPlayer player) { return; }
            activePlayers.Remove(player);
        }

        private void GrantPlayerSpellEffect(IPlayer player)
        {
            switch (owner.CreatureId)
            {
                case (uint) ObjectiveEntities.ExtraBooster:
                {
                    GrantPlayerSpellEffect(player, QuestSpells.ExtraGas);
                    GrantPlayerSpellEffect(player, QuestSpells.PowerBoostProxy0);
                    break;
                }
                case (uint) ObjectiveEntities.Holoring:
                {
                    GrantPlayerSpellEffect(player, QuestSpells.PowerBoostProxy0);
                    break;
                }
            }
        }
        
        private void GrantPlayerSpellEffect(IPlayer player, QuestSpells spellEffect)
        {
            ISpellParameters parameters = spellParameterFactory.Resolve();
            parameters.UserInitiatedSpellCast = false;
            player.CastSpell((uint) spellEffect, parameters);
        }
    }
}
