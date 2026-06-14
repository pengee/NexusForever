using NexusForever.Game.Abstract.Map.Search;
using NexusForever.Script.Template.Filter;
using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Script.Template;
using System.Numerics;
using NexusForever.Game.Map.Search;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

namespace NexusForever.Script.Main.AI
{
    /// <summary>
    /// Default combat AI for testing
    /// </summary>
    [ScriptFilterDefault]
    public class DefaultCombatAI : IOwnedScript<ICreatureEntity>, IUnitScript
    {
        public bool IsEnabled { get; private set; } = true;
        public void Disable() => IsEnabled = false;
        public void Enable() => IsEnabled = true;
        
        protected ICreatureEntity owner;
        private readonly Dictionary<uint, double> spellCooldowns = new();
        private const float AggroRange = 15f;
        private const float ChaseSpeed = 5f;
        private const float EntityRadius = 1f;
        private const float UpdateFrequency = 1f;

        private IUnitEntity currentTarget;
        private float lastTargetDistance;
        private bool hadTarget;
        private bool returningToHome;
        
        private bool ownerHasMovementCC;
        private bool ownerHasSpellCC;
        
        private double lastUpdate;

        private class SearchCheckAggro : ISearchCheck<IPlayer>
        {
            private readonly ICreatureEntity owner;

            public SearchCheckAggro(ICreatureEntity owner) => this.owner = owner;

            public bool CheckEntity(IPlayer player) => Vector3.Distance(owner.Position, player.Position) <= AggroRange;
        }
        
        public void EnemyAlert(IPlayer player)
        {
            if(owner.ThreatManager.Any(t=> t.HatedUnitId == player.ControlGuid)) { return; }
            owner.ThreatManager.UpdateThreat(player, 100);
            Update(lastUpdate - UpdateFrequency); //force update
        }

        private void UpdateCCState()
        {
            ownerHasMovementCC = owner.HasMovementCC();
            ownerHasSpellCC = owner.HasSpellCC();
        }

        public void OnLoad(ICreatureEntity entityOwner) => owner = entityOwner;
        public void OnThreatAddTarget(IHostileEntity hostile) => SelectTarget();
        public void OnThreatRemoveTarget(IHostileEntity hostile) => SelectTarget();

        public void OnThreatChange(IHostileEntity hostile)
        {
            if(!owner.InCombat){ return; }
            SelectTarget();
        }

        public void OnDeath() => ClearAllStates();

        public void OnRespawn()
        {
            ClearAllStates();

            // Defensive: if OnDeath didn't fire (AI was disabled) the target/threat
            // may still be set. Clear both so we don't re-aggro the killer on spawn.
            if (owner.TargetGuid.HasValue)
            {
                ClearThreatTarget();
            }
            else
            {
                owner.ThreatManager.ClearThreatList();
            }
        }

        private void ClearAllStates()
        {
            currentTarget = null;
            lastTargetDistance = 0f;
            hadTarget = false;
            returningToHome = false;
            spellCooldowns.Clear();
        }

        public void ClearThreatTarget()
        {
            owner.ThreatManager.ClearThreatList();
            owner.SetTarget((IWorldEntity) null);
            hadTarget = false;
        }

        protected virtual void SelectTarget()
        {
            IHostileEntity hostile = owner.ThreatManager.GetTopHostile();
            if (hostile == null)
            {
                owner.SetTarget((IWorldEntity) null);
                return;
            }

            owner.SetTarget(hostile.HatedUnitId, hostile.Threat);
        }
        
        private void FindThreat()
        {
            if (owner.ThreatManager.IsThreatened) { return; }
            
            //update threat
            foreach (IPlayer player in owner.Map.Search(owner.Position, AggroRange, new SearchCheckAggro(owner)))
            {
                if (!owner.CanAttack(player)) continue;
                // threat value is placeholder
                owner.ThreatManager.UpdateThreat(player, 100);
                AlertNeighboringEntities(player);
            }
        }

        private void AlertNeighboringEntities(IPlayer player)
        {
            //get like entities in proximity
            IEnumerable<IWorldEntity> neighbors = owner.Map.Search(owner.Position, AggroRange * 0.5f, new SearchCheckRange<IWorldEntity>(owner.Position, AggroRange * 0.5f)).Where(a => a.CreatureId == owner.CreatureId);
            
            //alert
            foreach (IWorldEntity neighbor in neighbors)
            {
                neighbor.InvokeScriptCollection<DefaultCombatAI>(s => s.EnemyAlert(player));
            }
        }

        private void StopMovement()
        {
            returningToHome = false;
            owner.MovementManager.SetStateDefault();
        }

        public void Update(double lastTick)
        {
            if (!owner.IsAlive || !IsEnabled)
            {
                if (owner.TargetGuid.HasValue)
                {
                    ClearAllStates();
                }
                return;
            }
            
            double currentTime = TimeSpan.FromTicks(DateTime.UtcNow.Ticks).TotalSeconds;
            
            if (currentTime - lastUpdate < UpdateFrequency){ return; }
            lastUpdate = currentTime;
            
            OutOfCombatUpdate();

            UpdateSpellCooldowns(lastTick);

            UpdateCCState();

            UpdateMovement();

            Attack();

            CheckLeashRange();
        }

        private void Attack()
        {
            if (ownerHasSpellCC || returningToHome) { return; }
            
            if (CastSpell(currentTarget, lastTargetDistance)) { return; }

            if (!(lastTargetDistance > EntityRadius) || ownerHasMovementCC) { return; }
            
            Vector3 direction = Vector3.Normalize(currentTarget.Position - owner.Position);
            owner.MovementManager.MoveToPosition(currentTarget.Position - (direction * EntityRadius * 2), ChaseSpeed);
        }
        
        private void UpdateMovement()
        {
            if (ownerHasMovementCC)
            {
                StopMovement();
                return;
            }

            if (returningToHome)
            {
                if (Vector3.Distance(owner.Position, owner.LeashPosition) <= 1f)
                {
                    owner.MovementManager.SetStateDefault();
                    ClearAllStates();
                }
                else
                {
                    return;
                }
            }
            
            FindThreat();

            bool hasTarget = owner.TargetGuid.HasValue;
            if (hasTarget) { hadTarget = true; }

            if (!hasTarget)
            {
                // Only stop movement on the transition from had-target to no-target, not every tick
                if (hadTarget)
                {
                    hadTarget = false;
                    ReturnHome();
                }

                return;
            }
            
            // only happens if we have a target
            currentTarget = owner.GetVisible<IUnitEntity>(owner.TargetGuid.Value);
            if (!currentTarget.IsAlive)
            {
                owner.SetTarget((IWorldEntity) null);
                ReturnHome();
                return;
            }

            lastTargetDistance = Vector3.Distance(owner.Position, currentTarget.Position);
            if (lastTargetDistance > AggroRange)
            {
                owner.ThreatManager.ClearThreatList();
                owner.SetTarget((IWorldEntity)null);
                ReturnHome();
            }
            
            //Console.Error.WriteLine($"[COMBAT] Entity {owner.EntityId} movement update failed (NO TARGET)");
        }

        private void UpdateSpellCooldowns(double lastTick)
        {
            foreach (uint key in new List<uint>(spellCooldowns.Keys))
            {
                spellCooldowns[key] -= lastTick;
                if (spellCooldowns[key] <= 0d)
                    spellCooldowns.Remove(key);
            }
        }

        private void OutOfCombatUpdate()
        {
            if (owner.InCombat) { return; }
            
            if (owner.Health < owner.MaxHealth)
            {
                owner.ModifyHealth((uint) MathF.Round(owner.MaxHealth * 0.05f), DamageType.Heal,  owner);
            }

            if (owner.Shield < owner.MaxShieldCapacity)
            {
                owner.Shield += (uint) MathF.Round(owner.MaxShieldCapacity * 0.05f);
            }

            if (owner.TargetGuid.HasValue)
            {
                ClearThreatTarget();
            }
        }

        private void ReturnHome()
        {
            if(ownerHasMovementCC){ return; }
            
            if (Vector3.Distance(owner.Position, owner.LeashPosition) > 1)
            {
                if (returningToHome) { return; }
                
                returningToHome = true;
                ClearThreatTarget();
                owner.MovementManager.MoveToPosition(owner.LeashPosition, ChaseSpeed * 0.75f);
            }
            else if (returningToHome)
            {
                StopMovement();
            }
        }

        private bool CastSpell(IUnitEntity target, float distance)
        {
            if (ownerHasSpellCC) { return false; }
            
            foreach (ICreatureSpellEntry spell in owner.CreatureSpells)
            {
                if (!spellCooldowns.TryGetValue(spell.Spell4BaseId, out double remaining) || remaining > 0d)
                {
                    Console.Error.WriteLine($"[COMBAT] Entity {owner.EntityId} spell {spell.Spell4BaseId} -> cooldown {remaining} seconds");
                    continue;
                }

                if (distance < spell.MinRange || distance > spell.MaxRange)
                {
                    Console.Error.WriteLine($"[COMBAT] Entity {owner.EntityId} spell {spell.Spell4BaseId} -> out of range {distance} : {spell.MinRange} <-> {spell.MaxRange}");
                    continue;
                }

                owner.CastSpell(spell.Spell4BaseId, 0, target.Guid);
                spellCooldowns[spell.Spell4BaseId] = 2d;
                Console.Error.WriteLine($"[COMBAT] Entity {owner.EntityId} casting  spell {spell.Spell4BaseId} on {target.Guid}");
                return true;
            }
            
            return false;
        }

        private void CheckLeashRange()
        {
            if(ownerHasMovementCC) { return; }
            //check leash range
            if (!(Vector3.Distance(owner.Position, owner.LeashPosition) > owner.LeashRange)) return;
            //return to leash position
            ReturnHome();
        }
    }
}