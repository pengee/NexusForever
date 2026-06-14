using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Game.Abstract.Map.Search;
using NexusForever.Script.Template.Filter;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Network.World.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Static.Spell;
using NexusForever.Script.Template;
using NexusForever.Game.Entity;
using NexusForever.Shared;
using System.Numerics;
using NexusForever.Game.Spell;
using NexusForever.GameTable.Model;


namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    [ScriptFilterCreatureId((uint) ObjectiveEntities.DominionFactionTurret, (uint) ObjectiveEntities.ExileFactionTurret)]
    public class TutorialP2TurretScript : IOwnedScript<IAiTurretEntity>, IUnitScript
    {
        private const float UpdateFrequency = 1f;
        
        private readonly Dictionary<QuestSpells, Spell4Entry> turretSpells = new();
        
        private IAiTurretEntity owner;
        private IPlayer targetPlayer;

        private float aggroRange;
        
        private bool active;
        private double lastUpdate;
        private bool doMortarAttack = true;
        private bool stunnedByPlayer;
        
        #region Dependency Injection
        private readonly IFactory<ISpellParameters> spellParameterFactory;
        public TutorialP2TurretScript(IFactory<ISpellParameters> spellParameterFactory) => this.spellParameterFactory = spellParameterFactory;
        #endregion
        
        private class SearchCheckAggro(ICreatureEntity owner, float range) : ISearchCheck<IPlayer>
        {
            public bool CheckEntity(IPlayer player) => Vector3.Distance(owner.Position, player.Position) <= range;
        }
        
        private bool OwnerIsCastingSpell() => ((UnitEntity) owner).GetActiveSpell(s => s.IsCasting || s.IsExecuting) != null;

        private bool OwnerHasImmunityBuff() => owner.BuffManager.GetBuffs().Any(b => b.EffectEntry.EffectType == SpellEffectType.SpellEffectImmunity);
        
        private IPlayer GetPlayer() => owner.Map.Search(owner.Position, aggroRange, new SearchCheckAggro(owner, aggroRange)).FirstOrDefault();

        private ISpellParameters GetAimedSpellParameters(IPlayer target)
        {
            if(target == null) { return null; }
            
            ISpellParameters parameters = spellParameterFactory.Resolve();
            parameters.PrimaryTargetId = target.Guid;
            parameters.Yaw = FaceTargetPosition(target.Position); // if we don't set on aimed it goes z-forward to start?
            return parameters;
        }

        private ISpellParameters GetPositionedSpellParameters(IPlayer player, Vector3 position)
        {
            ISpellParameters parameters = spellParameterFactory.Resolve();
            if (player != null)
            {
                parameters.PrimaryTargetId = player.Guid;
                parameters.AttachedUnitId = player.Guid;
                FaceTargetPosition(player.Position);
            }
            else if (position != Vector3.Zero)
            {
                parameters.TelegraphPositions = [new Position(position)];
                FaceTargetPosition(position);
            }
            
            return parameters;
        }
        
        private QuestSpells GetChargeSpell() => owner.CreatureId == (uint)ObjectiveEntities.ExileFactionTurret
            ? QuestSpells.ExileTurretChargeAttack
            : QuestSpells.DominionTurretChargeAttack;
        
        public void OnLoad(IAiTurretEntity entityOwner) => owner = entityOwner;

        public void OnAddToMap(IBaseMap map)
        {
            //get spells
            turretSpells.Add(QuestSpells.TurretImmune, QuestSpells.TurretImmune.GetSpellEntry());
            turretSpells.Add(QuestSpells.TurretMortarAttack, QuestSpells.TurretMortarAttack.GetSpellEntry());
            turretSpells.Add(GetChargeSpell(), GetChargeSpell().GetSpellEntry());

            aggroRange = MathF.Max(turretSpells[GetChargeSpell()].TargetMaxRange, turretSpells[QuestSpells.TurretMortarAttack].TargetMaxRange) * 2; //10 is a little small, no?
            
            owner.SetInRangeCheck(aggroRange);
        }

        public void OnEnterRange(IGridEntity entity)
        {
            if(entity is IPlayer newPlayer && owner.CanAttack(newPlayer))
            {
                active = true;
            }
        }

        public void OnExitRange(IGridEntity entity)
        {
            if(entity is IPlayer newPlayer && owner.CanAttack(newPlayer))
            {
                active = false;
            }
        }

        private bool DoUpdate(double lastTick)
        {
            double currentTime = TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalSeconds;
            if (currentTime - lastUpdate < UpdateFrequency){ return false; }
            lastUpdate = currentTime;
            return true;
        }

        private void CheckStunnedState()
        {
            if (stunnedByPlayer || !owner.HasCCState(CCState.Stun)) return;
            stunnedByPlayer = true;
            RemoveOwnerImmunityBuff();
        }
        
        public void Update(double lastTick)
        {
            if (!owner.IsAlive || !active) { return; }
            
            //check every update
            CheckStunnedState();
            
            //throttle attack rate
            if(!DoUpdate(lastTick)) {return;}
            
            // update buff state
            if (!stunnedByPlayer && !OwnerHasImmunityBuff())
            {
                GrantOwnerImmunityBuff();
                return;
            }
            
            //stop if casting
            if (OwnerIsCastingSpell()) { return; }
            
            targetPlayer = GetPlayer();

            if (targetPlayer == null) { return; }
            // try mortar
            if (doMortarAttack)
            {
                DoMortarAttack(targetPlayer);
                doMortarAttack = false;
                return;
            }
                
            //try charge
            DoChargeAttack(targetPlayer);
            doMortarAttack = true;
        }
        
        private float FaceTargetPosition(Vector3 target)
        {
            Vector3 targetDirection = (owner.Position - target); // this is backwards for some reason
            float targetYaw = MathF.Atan2(targetDirection.X, targetDirection.Z);
            owner.MovementManager.SetRotation(new Vector3(targetYaw, 0, 0), true);
            return targetYaw;
        }
        
        private void GrantOwnerImmunityBuff() => owner.CastSpell((uint) QuestSpells.TurretImmune, new SpellParameters(){PrimaryTargetId = owner.Guid});

        private void RemoveOwnerImmunityBuff() => owner.BuffManager.GetBuffs().FirstOrDefault(b => b.EffectEntry.EffectType == SpellEffectType.SpellEffectImmunity)?.Expire();
        
        private void DoChargeAttack(IPlayer target) => owner.CastSpell((uint) GetChargeSpell(), GetAimedSpellParameters(target));
        
        private void DoMortarAttack(IPlayer target) => owner.CastSpell((uint) QuestSpells.TurretMortarAttack, GetPositionedSpellParameters(null, target.Position));
    }
}