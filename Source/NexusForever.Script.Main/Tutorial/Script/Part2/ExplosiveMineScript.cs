using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template.Filter;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Map.Search;
using NexusForever.Script.Template;
using NexusForever.Shared.Game;
using NexusForever.Game.Spell;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    /// <summary>
    /// Mine detonation chain
    /// Mine is type 32 -> entity, not creature
    /// So no direct spells and can't cast spells
    /// </summary>
    [ScriptFilterCreatureId((uint) ObjectiveEntities.ExplosiveMineSmall, (uint) ObjectiveEntities.ExplosiveMineMed, (uint) ObjectiveEntities.ExplosiveMineBig)]
    public class ExplosiveMineScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private IWorldEntity owner;
        private ICreatureEntity caster;
        private IPlayer targetPlayer;
        
        private UpdateTimer despawnTimer;
        private bool activationComplete;
        private bool detonationComplete;
        
        private static ushort GetQuestId(IPlayer player) => player.Faction2 == Faction.Exile
            ? (ushort) TutorialReferences.TheFaceOfTheEnemyExile //ExileExplosiveMine
            : (ushort) TutorialReferences.TheFaceOfTheEnemyDominion;
        
        private static byte GetQuestObjective(uint creature) => creature switch
        {
            (uint)ObjectiveEntities.ExplosiveMineSmall => 1,
            (uint)ObjectiveEntities.ExplosiveMineMed => 2,
            (uint)ObjectiveEntities.ExplosiveMineBig => 3,
            _ => 0
        };
        
        private static QuestSpells GetDetonationSpell(uint creatureId) => creatureId switch
        {
            (uint) ObjectiveEntities.ExplosiveMineSmall => QuestSpells.MineDetonateEasy,
            (uint) ObjectiveEntities.ExplosiveMineMed => QuestSpells.MineDetonateMed,
            (uint) ObjectiveEntities.ExplosiveMineBig => QuestSpells.MineDetonateHard,
            _ => QuestSpells.MineDetonateEasy
        };

        private IWorldEntity GetBeaconArrow() => owner.Map.Search(owner.Position, 25,
                new SearchCheckRange<IWorldEntity>(owner.Position with { Y = owner.Position.Y + 10 },
                    25)).FirstOrDefault(a => a.CreatureId == (uint) ObjectiveEntities.BeaconArrow);
        
        private static void CastDismantle(IPlayer player) => player.CastSpell((uint) QuestSpells.MineDismantle, new SpellParameters { UserInitiatedSpellCast = true });
        
        private static void CastDetonate(ICreatureEntity caster, QuestSpells detonateSpell) => caster.CastSpell((uint) detonateSpell, new SpellParameters { PrimaryTargetId = caster.Guid});
        
        //TODO: remove timer and check is casting or timestamp
        private bool IsTimerComplete(double lastTick)
        {
            despawnTimer?.Update(lastTick);
            if (despawnTimer?.HasElapsed != true) return false;
            despawnTimer = null;
            return true;
        }
        
        public void OnLoad(IWorldEntity entityOwner) => owner = entityOwner;

        //
        public void OnAddToMap(IBaseMap map)
        {
            caster = ScriptHelpers.CreateInvisibleEntity(owner.Map, owner.Position);
            caster.SetFaction(owner.Faction2);
            caster.ModifyHealth(caster.MaxHealth, DamageType.Heal, caster);
        }

        public void OnRemoveFromMap(IBaseMap map)
        {
            if(caster == null) { return; }
            caster.RemoveFromMap();
            caster.Dispose();
            caster = null;
        }

        public void OnActivate(IPlayer player)
        {
            targetPlayer = player;
            activationComplete = true;
            CastDismantle(player);
            despawnTimer = new UpdateTimer(QuestSpells.MineDismantle.SpellCastTime() / 1000f);
        }
        
        public void Update(double lastTick)
        {
            HandleActivationCompleted(lastTick);
            HandleDetonationCompleted(lastTick);
        }
        
        private void CompletePlayerQuest(IPlayer player) => player.QuestManager.QuestAchieveObjective(GetQuestId(targetPlayer), GetQuestObjective(owner.CreatureId));
        
        private void HandleActivationCompleted(double lastTick)
        {
            if(!activationComplete) { return; }
            
            if(!IsTimerComplete(lastTick)) { return;}

            if (targetPlayer == null)
            {
                activationComplete = false;
                return;
            }
            
            CompletePlayerQuest(targetPlayer);
            GetBeaconArrow()?.RemoveFromMap();
            
            // we should move this to the player now that it should be supported
            CastDetonate(caster, GetDetonationSpell(owner.CreatureId));
            
            float castTime = GetDetonationSpell(owner.CreatureId).SpellCastTime() / 1000f + 0.2f;
            despawnTimer = new UpdateTimer(castTime);

            activationComplete = false;
            detonationComplete = true;
        }

        private void HandleDetonationCompleted(double lastTick)
        {
            if(!detonationComplete){ return; }
            if(!IsTimerComplete(lastTick)) { return;}
            owner.DisplayInfo = 0;
        }
    }
}