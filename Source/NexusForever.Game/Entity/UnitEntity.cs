using System.Numerics;
using NLog;
using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Movement;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Combat;
using NexusForever.Game.Spell;
using NexusForever.Game.Static;
using NexusForever.Game.Static.Achievement;
using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Loot;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Combat;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Entity;
using NexusForever.Network.World.Message.Model.Loot;
using NexusForever.Network.World.Message.Static;
using NexusForever.Script.Template;
using NexusForever.Shared.Game;

namespace NexusForever.Game.Entity
{
    public abstract class UnitEntity : WorldEntity, IUnitEntity
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private class DRState
        {
            public int HitCount;
            public double CooldownRemaining;
            public const double CooldownDuration = 18d;
        }

        public float HitRadius { get; protected set; } = 1f;

        /// <summary>
        /// Guid of the <see cref="IUnitEntity"/> currently targeted.
        /// </summary>
        public uint? TargetGuid { get; private set; }

        /// <summary>
        /// Determines whether or not this <see cref="IUnitEntity"/> is alive.
        /// </summary>
        public bool IsAlive => Health > 0u && deathState == null;

        protected EntityDeathState? DeathState
        {
            get => deathState;
            set
            {
                deathState = value;

                if (deathState is null or EntityDeathState.JustDied)
                {
                    EnqueueToVisible(new ServerEntityDeathState
                    {
                        UnitId    = Guid,
                        Dead      = !IsAlive,
                        Reason    = 0, // client does nothing with this value
                        RezHealth = IsAlive ? Health : 0u
                    }, true);
                }
            }
        }

        private EntityDeathState? deathState;

        /// <summary>
        /// Determines whether or not this <see cref="IUnitEntity"/> is in combat.
        /// </summary>
        public bool InCombat
        {
            get => inCombat;
            private set
            {
                if (inCombat == value)
                    return;

                inCombat = value;

                if (value)
                    ProcManager?.EvaluateProc(ProcTriggerType.OnEnterCombat);
                else
                    ProcManager?.EvaluateProc(ProcTriggerType.OnLeaveCombat);

                EnqueueToVisible(new ServerUnitEnteredCombat
                {
                    UnitId   = Guid,
                    InCombat = value
                }, true);
            }
        }

        private bool inCombat;

        public IThreatManager ThreatManager { get; private set; }
        public IBuffManager BuffManager { get; private set; }
        public IProcManager ProcManager { get; private set; }

        public bool IsStealthed => stealthed;
        private bool stealthed;
        public float StealthLevel { get; private set; }

        public uint Absorption { get; set; }
        public uint HealingAbsorption { get; set; }

        public uint InterruptArmorBase
        {
            get => (uint)GetPropertyValue(Property.InterruptArmorThreshold);
        }

        private readonly HashSet<CCState> ccStates = new();

        private struct CCStateExpiry
        {
            public double Duration;
            public uint CastingId;
            public uint EffectId;
        }
        private readonly Dictionary<CCState, CCStateExpiry> ccStateDurations = new();

        // Sampled log accumulator for TickCCStates diagnostic output
        private double ccStateLogAccumulator;

        private readonly Dictionary<ushort, DRState> drStates = new();
        private readonly HashSet<uint> spellImmunities = new();
        private readonly HashSet<SpellEffectType> spellEffectImmunities = new();

        private double iaRechargeTimer;
        private uint iaRechargeCount;
        private bool iaRechargePending;

        /// <summary>
        /// Initial stab at a timer to regenerate Health & Shield values.
        /// </summary>
        private UpdateTimer statUpdateTimer = new UpdateTimer(0.25); // TODO: Long-term this should be absorbed into individual timers for each Stat regeneration method

        private readonly List<ISpell> pendingSpells = new();

        private Dictionary<Property, Dictionary</*spell4Id*/uint, ISpellPropertyModifier>> spellProperties = new();
        private readonly HashSet<(uint Spell4BaseId, uint EffectIndex)> suppressedSpellEffects = new();

        #region Dependency Injection

        public UnitEntity(IMovementManager movementManager)
            : base(movementManager)
        {
            ThreatManager = new ThreatManager(this);
            BuffManager = new BuffManager(this);
            ProcManager = new ProcManager(this);

            InitialiseHitRadius();
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();

            BuffManager.RemoveAllBuffs();
            ProcManager.RemoveAllProcs();
            RemoveAllCCStates();

            foreach (ISpell spell in pendingSpells)
                spell.Dispose();
        }

        private void InitialiseHitRadius()
        {
            if (CreatureEntry == null)
                return;

            Creature2ModelInfoEntry modelInfoEntry = GameTableManager.Instance.Creature2ModelInfo.GetEntry(CreatureEntry.Creature2ModelInfoId);
            if (modelInfoEntry != null)
                HitRadius = modelInfoEntry.HitRadius * CreatureEntry.ModelScale;
        }

        public override void Update(double lastTick)
        {
            base.Update(lastTick);

            foreach (ISpell spell in pendingSpells.ToArray())
            {
                spell.Update(lastTick);
                if (spell.IsFinished)
                    pendingSpells.Remove(spell);
            }

            statUpdateTimer.Update(lastTick);
            if (statUpdateTimer.HasElapsed)
            {
                HandleStatUpdate(lastTick);
                statUpdateTimer.Reset();
            }

            BuffManager.Update(lastTick);
            ProcManager.Update(lastTick);
            UpdateDR(lastTick);
            TickCCStates(lastTick);
            UpdateIARecharge(lastTick);
        }

        /// <summary>
        /// Remove tracked <see cref="IGridEntity"/> that is no longer in vision range.
        /// </summary>
        protected override void RemoveVisible(IGridEntity entity)
        {
            if (entity.Guid == TargetGuid)
                SetTarget((IWorldEntity)null);

            ThreatManager.RemoveHostile(entity.Guid);

            base.RemoveVisible(entity);
        }

        /// <summary>
        /// Add a <see cref="Property"/> modifier given a Spell4Id and <see cref="ISpellPropertyModifier"/> instance.
        /// </summary>
        public void AddSpellModifierProperty(ISpellPropertyModifier spellModifier, uint spell4Id)
        {
            if (spellProperties.TryGetValue(spellModifier.Property, out Dictionary<uint, ISpellPropertyModifier> spellDict))
            {
                if (spellDict.ContainsKey(spell4Id))
                    spellDict[spell4Id] = spellModifier;
                else
                    spellDict.Add(spell4Id, spellModifier);
            }
            else
            {
                spellProperties.Add(spellModifier.Property, new Dictionary<uint, ISpellPropertyModifier>
                {
                    { spell4Id, spellModifier }
                });
            }

            CalculateProperty(spellModifier.Property);
        }

        /// <summary>
        /// Remove a <see cref="Property"/> modifier by a Spell that is currently affecting this <see cref="IUnitEntity"/>.
        /// </summary>
        public void RemoveSpellProperty(Property property, uint spell4Id)
        {
            if (spellProperties.TryGetValue(property, out Dictionary<uint, ISpellPropertyModifier> spellDict))
                spellDict.Remove(spell4Id);

            CalculateProperty(property);
        }

        /// <summary>
        /// Remove all <see cref="Property"/> modifiers by a Spell that is currently affecting this <see cref="IUnitEntity"/>
        /// </summary>
        public void RemoveSpellProperties(uint spell4Id)
        {
            List<Property> propertiesWithSpell = spellProperties.Where(i => i.Value.ContainsKey(spell4Id)).Select(p => p.Key).ToList();

            foreach (Property property in propertiesWithSpell)
                RemoveSpellProperty(property, spell4Id);
        }

        /// <summary>
        /// Returns whether the <see cref="IUnitEntity"/> currently has the supplied <see cref="CCState"/> active.
        /// </summary>
        public bool HasCCState(CCState state)
        {
            return ccStates.Contains(state);
        }

        public bool HasMovementCC() => CCHelper.MovementCCStates.Any(HasCCState);
        public bool HasSpellCC() => CCHelper.SpellCCStates.Any(HasCCState);

        /// <summary>
        /// Apply a <see cref="CCState"/> to this <see cref="IUnitEntity"/> for the supplied duration (in seconds).
        /// </summary>
        /// <remarks>
        /// The unit tracks its own expiry for the <see cref="CCState"/>, so it falls off when the timer
        /// reaches zero regardless of whether an associated buff exists.
        /// </remarks>
        public void ApplyCCState(CCState state, uint castingId, uint effectId, IUnitEntity caster = null, double duration = 0d)
        {
            ccStates.Add(state);

            if (duration > 0d)
                ccStateDurations[state] = new CCStateExpiry
                {
                    Duration  = duration,
                    CastingId = castingId,
                    EffectId  = effectId
                };

            CancelChanneledSpells();

            caster?.ProcManager?.EvaluateProc(ProcTriggerType.OnCCApplied, this);

            EnqueueToVisible(new ServerEntityCCStateSet
            {
                UnitId             = Guid,
                CCType             = state,
                SpellEffectUniqueId = effectId
            }, true);

            log.Info($"ApplyCCState UnitId={Guid} CCState={state} CastingId={castingId} EffectId={effectId} Duration={duration}");
        }

        /// <summary>
        /// Remove a <see cref="CCState"/> from this <see cref="IUnitEntity"/>.
        /// </summary>
        public void RemoveCCState(CCState state, uint castingId, uint effectId)
        {
            RemoveCCState(state, castingId, effectId, "explicit");
        }

        /// <summary>
        /// Internal overload with a <paramref name="reason"/> for diagnostic logging.
        /// </summary>
        private void RemoveCCState(CCState state, uint castingId, uint effectId, string reason)
        {
            ccStateDurations.Remove(state);

            if (!ccStates.Remove(state))
                return;

            EnqueueToVisible(new ServerEntityCCStateRemove
            {
                UnitId              = Guid,
                CCType              = state,
                SpellCastUniqueId   = castingId,
                SpellEffectUniqueId = effectId,
                Removed             = true
            }, true);

            float rechargeTime = GetPropertyValue(Property.InterruptArmorAfterCCRechargeTime);
            uint rechargeCount = (uint)GetPropertyValue(Property.InterruptArmorAfterCCRechargeCount);

            if (rechargeTime > 0f && rechargeCount > 0u)
            {
                iaRechargeTimer = rechargeTime;
                iaRechargeCount = rechargeCount;
                iaRechargePending = true;
            }

            // Expire any active CCStateSet buffs that were tracking this state so they don't linger as zombies.
            foreach (IBuff buff in BuffManager.GetBuffs(b =>
                b.EffectEntry.EffectType == SpellEffectType.CCStateSet &&
                (CCState)b.EffectEntry.DataBits00 == state &&
                !b.IsExpired).ToList())
            {
                buff.Expire();
            }

            log.Info($"RemoveCCState UnitId={Guid} CCState={state} CastingId={castingId} EffectId={effectId} Reason={reason}");
        }

        /// <summary>
        /// Remove all active <see cref="CCState"/> instances from this <see cref="IUnitEntity"/>.
        /// </summary>
        public void RemoveAllCCStates()
        {
            foreach (CCState state in ccStates.ToList())
            {
                EnqueueToVisible(new ServerEntityCCStateRemove
                {
                    UnitId  = Guid,
                    CCType  = state,
                    Removed = true
                }, true);
            }

            ccStates.Clear();
            ccStateDurations.Clear();
        }

        public void SetStealth(float level)
        {
            stealthed = true;
            StealthLevel = level;
        }

        public void RemoveStealth()
        {
            stealthed = false;
            StealthLevel = 0f;
        }

        public void SuppressSpellEffect(uint spell4BaseId, uint effectIndex)
        {
            suppressedSpellEffects.Add((spell4BaseId, effectIndex));
        }

        public void UnsuppressSpellEffect(uint spell4BaseId, uint effectIndex)
        {
            suppressedSpellEffects.Remove((spell4BaseId, effectIndex));
        }

        public bool IsSpellEffectSuppressed(uint spell4BaseId, uint effectIndex)
        {
            return suppressedSpellEffects.Contains((spell4BaseId, effectIndex));
        }

        public bool AggroImmune { get; private set; }

        public void SetAggroImmune(bool immune)
        {
            AggroImmune = immune;
        }

        public void AddSpellImmunity(uint spell4BaseId)
        {
            spellImmunities.Add(spell4BaseId);
        }

        public void RemoveSpellImmunity(uint spell4BaseId)
        {
            spellImmunities.Remove(spell4BaseId);
        }

        public bool IsSpellImmune(uint spell4BaseId)
        {
            return spellImmunities.Contains(spell4BaseId);
        }

        public void AddSpellEffectImmunity(SpellEffectType effectType)
        {
            spellEffectImmunities.Add(effectType);
        }

        public void RemoveSpellEffectImmunity(SpellEffectType effectType)
        {
            spellEffectImmunities.Remove(effectType);
        }

        public bool IsSpellEffectImmune(SpellEffectType effectType)
        {
            return spellEffectImmunities.Contains(effectType);
        }

        public bool DelayDeath { get; private set; }

        public void SetDelayDeath(bool delayDeath)
        {
            DelayDeath = delayDeath;
        }

        public CCStateApplyRulesResult CheckDiminishingReturns(ushort drCategoryId)
        {
            if (drCategoryId == 0)
                return CCStateApplyRulesResult.Ok;

            if (!drStates.TryGetValue(drCategoryId, out DRState state))
                return CCStateApplyRulesResult.Ok;

            if (state.CooldownRemaining <= 0d)
            {
                drStates.Remove(drCategoryId);
                return CCStateApplyRulesResult.Ok;
            }

            if (state.HitCount >= 3)
                return CCStateApplyRulesResult.DiminishingReturnsTriggerCap;

            return CCStateApplyRulesResult.Ok;
        }

        public float GetDRDurationModifier(ushort drCategoryId)
        {
            if (drCategoryId == 0)
                return 1.0f;

            if (!drStates.TryGetValue(drCategoryId, out DRState state))
                return 1.0f;

            if (state.CooldownRemaining <= 0d)
                return 1.0f;

            return state.HitCount switch
            {
                0 => 1.0f,
                1 => 0.5f,
                2 => 0.25f,
                _ => 0.0f
            };
        }

        public void RecordDRApplication(ushort drCategoryId)
        {
            if (drCategoryId == 0)
                return;

            if (!drStates.TryGetValue(drCategoryId, out DRState state))
            {
                state = new DRState();
                drStates.Add(drCategoryId, state);
            }

            state.HitCount++;
            state.CooldownRemaining = DRState.CooldownDuration;
        }

        public void UpdateDR(double lastTick)
        {
            List<ushort> expired = null;
            foreach (KeyValuePair<ushort, DRState> kv in drStates)
            {
                kv.Value.CooldownRemaining -= lastTick;
                if (kv.Value.CooldownRemaining <= 0d)
                {
                    expired ??= new List<ushort>();
                    expired.Add(kv.Key);
                }
            }

            if (expired != null)
                foreach (ushort key in expired)
                    drStates.Remove(key);
        }

        /// <summary>
        /// Tick down remaining durations for active <see cref="CCState"/>s and remove any that have expired.
        /// </summary>
        /// <remarks>
        /// This is the source of truth for <see cref="CCState"/> expiry. The unit-owned timer ensures the
        /// state falls off even when no tracking buff is present (e.g. StackGroup priority refusal, suspended
        /// buffs, or effects with <c>Duration == 0</c>).
        /// </remarks>
        private void TickCCStates(double lastTick)
        {
            if (ccStateDurations.Count == 0)
                return;

            List<CCState> expired = null;
            List<(CCState state, uint castingId, uint effectId)> expiredIds = null;
            foreach (KeyValuePair<CCState, CCStateExpiry> kv in ccStateDurations.ToList())
            {
                double remaining = kv.Value.Duration - lastTick;
                if (remaining <= 0d)
                {
                    expired ??= new List<CCState>();
                    expiredIds ??= new List<(CCState, uint, uint)>();
                    expired.Add(kv.Key);
                    expiredIds.Add((kv.Key, kv.Value.CastingId, kv.Value.EffectId));
                }
                else
                    ccStateDurations[kv.Key] = new CCStateExpiry
                    {
                        Duration  = remaining,
                        CastingId = kv.Value.CastingId,
                        EffectId  = kv.Value.EffectId
                    };
            }

            ccStateLogAccumulator += lastTick;
            if (ccStateLogAccumulator >= 1d)
            {
                ccStateLogAccumulator = 0d;
                string entries = string.Join(", ", ccStateDurations.Select(kv => $"{kv.Key}={kv.Value.Duration:F2}"));
                log.Info($"TickCCStates UnitId={Guid} Count={ccStateDurations.Count} Durations=[{entries}]");
            }

            if (expired == null)
                return;

            foreach ((CCState state, uint castingId, uint effectId) entry in expiredIds)
                RemoveCCState(entry.state, entry.castingId, entry.effectId, "timer");
        }

        private void UpdateIARecharge(double lastTick)
        {
            if (!iaRechargePending || !IsAlive)
                return;

            iaRechargeTimer -= lastTick;
            if (iaRechargeTimer > 0d)
                return;

            uint previousArmor = InterruptArmor;
            InterruptArmor = Math.Max(previousArmor, Math.Min(InterruptArmorBase, previousArmor + iaRechargeCount));
            uint restored = InterruptArmor - previousArmor;

            iaRechargePending = false;

            if (restored > 0)
            {
                EnqueueToVisible(new ServerCombatLog
                {
                    CombatLog = new CombatLogModifyInterruptArmor
                    {
                        Amount = restored,
                        CastData = new CombatLogCastData
                        {
                            CasterId = Guid,
                            TargetId = Guid,
                            SpellId = 0,
                            CombatResult = CombatResult.Hit
                        }
                    }
                }, true);
            }
        }

        /// <summary>
        /// Attempt to consume interrupt armor. Returns the number of interrupt armor points consumed.
        /// </summary>
        public uint ConsumeInterruptArmor()
        {
            uint currentArmor = InterruptArmor;
            if (currentArmor == 0u)
                return 0u;

            if (currentArmor == uint.MaxValue)
                return currentArmor;

            InterruptArmor = currentArmor - 1u;

            float rechargeTime = GetPropertyValue(Property.InterruptArmorRechargeTime);
            uint rechargeCount = (uint)GetPropertyValue(Property.InterruptArmorRechargeCount);

            if (rechargeTime > 0f && rechargeCount > 0u && !iaRechargePending)
            {
                iaRechargeTimer = rechargeTime;
                iaRechargeCount = rechargeCount;
                iaRechargePending = true;
            }

            return 1u;
        }

        /// <summary>
        /// Modify a <see cref="Vital"/> on this <see cref="IUnitEntity"/> by the supplied amount.
        /// </summary>
        public void ModifyVital(Vital vital, float amount)
        {
            switch (vital)
            {
                case Vital.Health:
                    if (amount >= 0f)
                        ModifyHealth((uint)amount, DamageType.Heal, null);
                    else
                        ModifyHealth((uint)(-amount), DamageType.Physical, null);
                    break;
                case Vital.ShieldCapacity:
                    if (amount >= 0f)
                        Shield = (uint)Math.Clamp(Shield + (uint)amount, 0u, MaxShieldCapacity);
                    else
                        Shield = (uint)Math.Clamp(Shield - (uint)(-amount), 0u, MaxShieldCapacity);
                    break;
                case Vital.InterruptArmor:
                    InterruptArmor = (uint)Math.Max(0u, InterruptArmor + (int)amount);
                    break;
                case Vital.Absorption:
                    if (amount >= 0f)
                        Absorption += (uint)amount;
                    else
                    {
                        uint reduction = (uint)(-amount);
                        Absorption = Absorption > reduction ? Absorption - reduction : 0u;
                    }
                    break;
                case Vital.HealingAbsorption:
                    if (amount >= 0f)
                        HealingAbsorption += (uint)amount;
                    else
                    {
                        uint reduction = (uint)(-amount);
                        HealingAbsorption = HealingAbsorption > reduction ? HealingAbsorption - reduction : 0u;
                    }
                    break;
                case Vital.Focus:
                {
                    Stat stat = Stat.Focus;
                    float current = GetStatFloat(stat) ?? 0f;
                    SetStat(stat, Math.Max(0f, current + amount));
                    break;
                }
                default:
                {
                    Stat? stat = GetStatForVital(vital);
                    if (stat != null)
                    {
                        float current = GetStatFloat(stat.Value) ?? 0f;
                        SetStat(stat.Value, Math.Max(0f, current + amount));
                    }
                    break;
                }
            }
        }

        public float GetVitalValue(Vital vital)
        {
            switch (vital)
            {
                case Vital.Health:
                    return Health;
                case Vital.ShieldCapacity:
                    return Shield;
                case Vital.InterruptArmor:
                    return InterruptArmor;
                case Vital.Absorption:
                    return Absorption;
                case Vital.HealingAbsorption:
                    return HealingAbsorption;
                case Vital.Focus:
                    return GetStatFloat(Stat.Focus) ?? 0f;
                default:
                {
                    Stat? stat = GetStatForVital(vital);
                    return stat != null ? GetStatFloat(stat.Value) ?? 0f : 0f;
                }
            }
        }

        private static Stat? GetStatForVital(Vital vital)
        {
            return vital switch
            {
                Vital.Resource0 => Stat.Resource0,
                Vital.Resource1 => Stat.Resource1,
                Vital.Resource2 => Stat.Resource2,
                Vital.Resource3 => Stat.Resource3,
                Vital.Resource4 => Stat.Resource4,
                Vital.Resource5 => Stat.Resource5,
                Vital.Resource6 => Stat.Resource6,
                _ => null
            };
        }

        /// <summary>
        /// Return all <see cref="IPropertyModifier"/> for this <see cref="IUnitEntity"/>'s <see cref="Property"/>
        /// </summary>
        private IEnumerable<ISpellPropertyModifier> GetSpellPropertyModifiers(Property property)
        {
            return spellProperties.ContainsKey(property) ? spellProperties[property].Values : Enumerable.Empty<ISpellPropertyModifier>();
        }

        protected override void CalculatePropertyValue(IPropertyValue propertyValue)
        {
            base.CalculatePropertyValue(propertyValue);

            // Run through spell adjustments first because they could adjust base properties
            // dataBits01 appears to be some form of Priority or Math Operator
            foreach (ISpellPropertyModifier spellModifier in GetSpellPropertyModifiers(propertyValue.Property)
                .OrderByDescending(s => s.Priority))
            {
                foreach (IPropertyModifier alteration in spellModifier.Alterations)
                {
                    // TODO: Add checks to ensure we're not modifying FlatValue and Percentage in the same effect?
                    switch (alteration.ModType)
                    {
                        case ModType.FlatValue:
                        case ModType.LevelScale:
                            propertyValue.Value += alteration.GetValue(Level);
                            break;
                        case ModType.Percentage:
                            propertyValue.Value *= alteration.GetValue();
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Handles regeneration of Stat Values. Used to provide a hook into the Update method, for future implementation.
        /// </summary>
        private void HandleStatUpdate(double lastTick)
        {
            if (!IsAlive)
                return;

            // TODO: This should probably get moved to a Calculation Library/Manager at some point. There will be different timers on Stat refreshes, but right now the timer is hardcoded to every 0.25s.
            // Probably worth considering an Attribute-grouped Class that allows us to run differentt regeneration methods & calculations for each stat.

            if (Health < MaxHealth)
                ModifyHealth((uint)(MaxHealth / 200f), DamageType.Heal, null);

            if (Shield < MaxShieldCapacity)
                Shield += (uint)(MaxShieldCapacity * GetPropertyValue(Property.ShieldRegenPct) * statUpdateTimer.Duration);
        }

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell id and <see cref="ISpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4Id, ISpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(spell4Id);
            if (spell4Entry == null)
                throw new ArgumentOutOfRangeException();

            CastSpell(spell4Entry.Spell4BaseIdBaseSpell, (byte)spell4Entry.TierIndex, parameters);
        }

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell base id, tier and primary target id.
        /// </summary>
        public void CastSpell(uint spell4BaseId, byte tier, uint primaryTargetId)
        {
            ISpellBaseInfo spellBaseInfo = GlobalSpellManager.Instance.GetSpellBaseInfo(spell4BaseId);
            if (spellBaseInfo == null)
                return;

            ISpellInfo spellInfo = spellBaseInfo.GetSpellInfo(tier);
            if (spellInfo == null)
            {
                Console.Error.WriteLine($"Failed to get spell info for {spell4BaseId}");
                return;
            }

            var parameters = new SpellParameters
            {
                SpellInfo             = spellInfo,
                UserInitiatedSpellCast = false,
                PrimaryTargetId       = primaryTargetId
            };

            CastSpell(parameters);
        }

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell base id, tier and <see cref="ISpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4BaseId, byte tier, ISpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            ISpellBaseInfo spellBaseInfo = GlobalSpellManager.Instance.GetSpellBaseInfo(spell4BaseId);
            if (spellBaseInfo == null)
                throw new ArgumentOutOfRangeException();

            ISpellInfo spellInfo = spellBaseInfo.GetSpellInfo(tier);
            if (spellInfo == null)
                throw new ArgumentOutOfRangeException();

            parameters.SpellInfo = spellInfo;
            CastSpell(parameters);
        }

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied <see cref="ISpellParameters"/>.
        /// </summary>
        public void CastSpell(ISpellParameters parameters)
        {
            if (!IsAlive)
                return;

            if (parameters == null)
                throw new ArgumentNullException();

            if (DisableManager.Instance.IsDisabled(DisableType.BaseSpell, parameters.SpellInfo.BaseInfo.Entry.Id))
            {
                if (this is IPlayer player)
                    player.SendSystemMessage($"Unable to cast base spell {parameters.SpellInfo.BaseInfo.Entry.Id} because it is disabled.");
                return;
            }

            if (DisableManager.Instance.IsDisabled(DisableType.Spell, parameters.SpellInfo.Entry.Id))
            {
                if (this is IPlayer player)
                    player.SendSystemMessage($"Unable to cast spell {parameters.SpellInfo.Entry.Id} because it is disabled.");
                return;
            }

            if (parameters.UserInitiatedSpellCast)
            {
                if (this is IPlayer player)
                    player.Dismount();
            }

            var spell = new Spell.Spell(this, parameters);
            spell.Cast();
            pendingSpells.Add(spell);

            if (parameters.UserInitiatedSpellCast)
                ProcManager?.EvaluateProc(ProcTriggerType.OnSpellCast);
        }

        /// <summary>
        /// Cancel any <see cref="ISpell"/>'s that are interrupted by movement.
        /// </summary>
        public void CancelSpellsOnMove()
        {
            bool channeledCancelled = false;
            foreach (ISpell spell in pendingSpells)
            {
                if (spell.IsMovingInterrupted() && (spell.IsCasting || spell.IsExecuting))
                {
                    if (spell.IsChanneled)
                        channeledCancelled = true;
                    spell.CancelCast(CastResult.CasterMovement);
                }
            }

            if (channeledCancelled)
                BuffManager.RemoveChanneledBuffs();
        }

        public void CancelChanneledSpells()
        {
            foreach (ISpell spell in pendingSpells)
                if (spell.IsChanneled && spell.IsExecuting)
                    spell.CancelCast(CastResult.SpellInterrupted);

            BuffManager.RemoveChanneledBuffs();
        }

        /// <summary>
        /// Cancel an <see cref="ISpell"/> based on its casting id.
        /// </summary>
        /// <param name="castingId">Casting ID of the spell to cancel</param>
        public void CancelSpellCast(uint castingId)
        {
            ISpell spell = pendingSpells.SingleOrDefault(s => s.CastingId == castingId);
            spell?.CancelCast(CastResult.SpellCancelled);
        }

        /// <summary>
        /// Returns an active <see cref="ISpell"/> that is affecting this <see cref="IUnitEntity"/>
        /// </summary>
        public ISpell GetActiveSpell(Func<ISpell, bool> func)
        {
            return pendingSpells.FirstOrDefault(func);
        }

        /// <summary>
        /// Determine if this <see cref="IUnitEntity"/> can attack supplied <see cref="IUnitEntity"/>.
        /// </summary>
        public virtual bool CanAttack(IUnitEntity target)
        {
            if (!IsAlive)
                return false;

            if (!target.IsValidAttackTarget() || !IsValidAttackTarget())
                return false;

            return GetDispositionTo(target.Faction1) < Disposition.Friendly;
        }

        /// <summary>
        /// Returns whether or not this <see cref="IUnitEntity"/> is an attackable target.
        /// </summary>
        public bool IsValidAttackTarget()
        {
            if (AggroImmune) { return false; }

            return (this is IPlayer or INonPlayerEntity or IAiTurretEntity);
        }

        /// <summary>
        /// Deal damage to this <see cref="IUnitEntity"/> from the supplied <see cref="IUnitEntity"/>.
        /// </summary>
        public void TakeDamage(IUnitEntity attacker, IDamageDescription damageDescription)
        {
            if (!IsAlive || !attacker.IsAlive)
                return;

            if (damageDescription == null)
            {
                log.Warn($"TakeDamage on {Guid} from {attacker.Guid} with null damageDescription, skipping");
                return;
            }

            // TODO: Calculate Threat properly
            if (ThreatManager == null)
                log.Warn($"TakeDamage on {Guid} from {attacker.Guid} with null ThreatManager, skipping threat update");
            else
                ThreatManager.UpdateThreat(attacker, (int)damageDescription.RawDamage);

            Shield -= damageDescription.ShieldAbsorbAmount;

            if (damageDescription.AbsorbedAmount > 0u)
            {
                uint remaining = damageDescription.AbsorbedAmount;
                Absorption = Absorption > remaining ? Absorption - remaining : 0u;

                foreach (IBuff buff in BuffManager.GetBuffs(b => b.EffectEntry.EffectType == SpellEffectType.Absorption && b.AbsorptionRemaining > 0u).ToList())
                {
                    uint deduct = Math.Min(buff.AbsorptionRemaining, remaining);
                    buff.AbsorptionRemaining -= deduct;
                    remaining -= deduct;

                    if (buff.AbsorptionRemaining == 0u)
                        BuffManager.RemoveBuff(buff);

                    if (remaining == 0u)
                        break;
                }
            }

            ModifyHealth(damageDescription.AdjustedDamage, damageDescription.DamageType, attacker);

            if (IsStealthed)
            {
                RemoveStealth();
                EnqueueToVisible(new ServerCombatLog
                {
                    CombatLog = new CombatLogStealth
                    {
                        UnitId   = Guid,
                        BExiting = true
                    }
                }, true);

                foreach (IBuff stealthBuff in BuffManager.GetBuffs(b =>
                             b.EffectEntry.EffectType == SpellEffectType.Stealth).ToList())
                {
                    BuffManager.RemoveBuff(stealthBuff);
                }
            }
        }

        /// <summary>
        /// Modify the health of this <see cref="IUnitEntity"/> by the supplied amount.
        /// </summary>
        /// <remarks>
        /// If the <see cref="DamageType"/> is <see cref="DamageType.Heal"/> amount is added to current health otherwise subtracted.
        /// </remarks>
        public virtual void ModifyHealth(uint amount, DamageType type, IUnitEntity source)
        {
            long newHealth = Health;
            if (type == DamageType.Heal)
            {
                newHealth += amount;

                if (source != null)
                {
                    source.ProcManager?.EvaluateProc(ProcTriggerType.OnHealDealt, this);
                    ProcManager?.EvaluateProc(ProcTriggerType.OnHealReceived, source);
                }
            }
            else
                newHealth -= amount;

            if (DelayDeath && newHealth <= 0)
                newHealth = 1;

            Health = (uint)Math.Clamp(newHealth, 0u, MaxHealth);

            if (Health == 0)
                OnDeath();
        }

        protected virtual void OnDeath()
        {
            DeathState = EntityDeathState.JustDied;

            foreach (ISpell spell in pendingSpells)
            {
                if (spell.IsCasting || (spell.IsChanneled && spell.IsExecuting))
                    spell.CancelCast(CastResult.CasterCannotBeDead);
            }

            // Stash the top hostile for kill credit before clearing the threat list
            IHostileEntity topHostile = ThreatManager.GetTopHostile();

            try
            {
                GenerateRewards();

                if (topHostile != null)
                {
                    IUnitEntity topThreat = GetVisible<IUnitEntity>(topHostile.HatedUnitId);
                    topThreat?.ProcManager?.EvaluateProc(ProcTriggerType.OnKill, this);
                }
            }
            catch (Exception ex)
            {
                log.Warn($"Exception during death reward generation for {Guid} (creature {CreatureId}): {ex.Message}");
            }

            // Clean up the dying entity's state. Runs after reward generation so the
            // threat list can still be iterated for quest/XP/loot credit, but is in
            // the same OnDeath so it still runs even if reward generation threw.
            ThreatManager.ClearThreatList();
            BuffManager.RemoveAllBuffs();
            ProcManager.RemoveAllProcs();
            RemoveAllCCStates();

            // Notify scripts so they can reset any script-local state (cached targets,
            // leashing flags, cooldowns) that would otherwise persist into the next life.
            scriptCollection?.Invoke<IUnitScript>(s => s.OnDeath());

            deathState = EntityDeathState.Dead;
        }

        private void GenerateRewards()
        {
            foreach (IHostileEntity hostile in ThreatManager)
            {
                IUnitEntity entity = GetVisible<IUnitEntity>(hostile.HatedUnitId);
                if (entity is IPlayer player)
                    RewardKiller(player);
            }
        }

        protected virtual void RewardKiller(IPlayer player)
        {
            player.QuestManager.ObjectiveUpdate(QuestObjectiveType.KillCreature, CreatureId, 1u);
            player.QuestManager.ObjectiveUpdate(QuestObjectiveType.KillCreature2, CreatureId, 1u);

            foreach (uint targetGroupId in AssetManager.Instance.GetTargetGroupsForCreatureId(CreatureId))
            {
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.KillTargetGroup, targetGroupId, 1u);
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.KillTargetGroups, targetGroupId, 1u);
            }

            RewardXp(player);
            RewardLoot(player);
            RewardAchievement(player);
        }

        private void RewardXp(IPlayer player)
        {
            if (this is IPlayer)
                return;

            Creature2Entry creatureEntry = CreatureEntry;
            if (creatureEntry == null)
                return;

            uint creatureLevel = Math.Clamp(creatureEntry.MaxLevel, creatureEntry.MinLevel, creatureEntry.MaxLevel);
            XpPerLevelEntry xpEntry = GameTableManager.Instance.XpPerLevel.GetEntry(creatureLevel);
            if (xpEntry == null)
                return;

            uint baseXp = xpEntry.BaseQuestXpPerLevel;

            Creature2DifficultyEntry difficultyEntry = GameTableManager.Instance.Creature2Difficulty.GetEntry(creatureEntry.Creature2DifficultyId);
            if (difficultyEntry != null)
                baseXp = (uint)(baseXp * difficultyEntry.UnitPropertyMultiplier[(uint)Property.BaseHealth]);

            Creature2TierEntry tierEntry = GameTableManager.Instance.Creature2Tier.GetEntry(creatureEntry.Creature2TierId);
            if (tierEntry != null)
                baseXp = (uint)(baseXp * tierEntry.UnitPropertyMultiplier[(uint)Property.BaseHealth]);

            player.XpManager.GrantXp(baseXp, ExpReason.KillCreature);
        }

        private void RewardLoot(IPlayer player)
        {
            if (this is IPlayer)
                return;

            Creature2Entry creatureEntry = CreatureEntry;
            if (creatureEntry == null)
                return;

            List<LootItem> lootItems = GenerateLootItems(creatureEntry);
            if (lootItems.Count > 0)
                player.LootManager.AddLoot(Guid, lootItems);
        }

        private List<LootItem> GenerateLootItems(Creature2Entry creatureEntry)
        {
            List<LootItem> items = new();

            uint creatureLevel = Math.Clamp(creatureEntry.MaxLevel, creatureEntry.MinLevel, creatureEntry.MaxLevel);
            CreatureLevelEntry levelEntry = GameTableManager.Instance.CreatureLevel.GetEntry(creatureLevel);

            float creditsMultiplier = 1f;
            Creature2DifficultyEntry difficultyEntry = GameTableManager.Instance.Creature2Difficulty.GetEntry(creatureEntry.Creature2DifficultyId);
            if (difficultyEntry != null)
                creditsMultiplier *= difficultyEntry.UnitPropertyMultiplier[(uint)Property.BaseHealth];

            Creature2TierEntry tierEntry = GameTableManager.Instance.Creature2Tier.GetEntry(creatureEntry.Creature2TierId);
            if (tierEntry != null)
                creditsMultiplier *= tierEntry.UnitPropertyMultiplier[(uint)Property.BaseHealth];

            float baseCredits = levelEntry != null ? levelEntry.UnitPropertyValue[(uint)Property.BaseHealth] : 0f;
            uint credits = (uint)(baseCredits * creditsMultiplier);

            if (credits > 0)
            {
                items.Add(new LootItem
                {
                    Type = LootItemType.Cash,
                    ItemId = (uint)CurrencyType.Credits,
                    Amount = credits,
                    CanLoot = true
                });
            }

            return items;
        }

        private void RewardAchievement(IPlayer player)
        {
            player.AchievementManager.CheckAchievements(player, AchievementType.KillCreatureEntry, CreatureId);

            foreach (uint targetGroupId in AssetManager.Instance.GetTargetGroupsForCreatureId(CreatureId))
                player.AchievementManager.CheckAchievements(player, AchievementType.KillCreatureGroup, targetGroupId);
        }

        /// <summary>
        /// Set target to supplied target guid.
        /// </summary>
        /// <remarks>
        /// A null target will clear the current target.
        /// </remarks>
        public void SetTarget(uint? target, uint threat = 0u)
        {
            SetTarget(target != null ? GetVisible<IWorldEntity>(target.Value) : null, threat);
        }

        /// <summary>
        /// Set target to supplied <see cref="IUnitEntity"/>.
        /// </summary>
        /// <remarks>
        /// A null target will clear the current target.
        /// </remarks>
        public virtual void SetTarget(IWorldEntity target, uint threat = 0u)
        {
            // notify current target they are no longer the target
            if (TargetGuid != null)
                GetVisible<IWorldEntity>(TargetGuid.Value)?.OnUntargeted(this);

            target?.OnTargeted(this);

            EnqueueToVisible(new ServerEntityTargetUnit
            {
                UnitId      = Guid,
                NewTargetId = target?.Guid ?? 0u,
                ThreatLevel = threat
            });

            TargetGuid = target?.Guid;
        }

        /// <summary>
        /// Invoked when a new <see cref="IHostileEntity"/> is added to the threat list.
        /// </summary>
        public virtual void OnThreatAddTarget(IHostileEntity hostile)
        {
            UpdateCombatState();
            scriptCollection?.Invoke<IUnitScript>(s => s.OnThreatAddTarget(hostile));
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is removed from the threat list.
        /// </summary>
        public virtual void OnThreatRemoveTarget(IHostileEntity hostile)
        {
            UpdateCombatState();
            scriptCollection?.Invoke<IUnitScript>(s => s.OnThreatRemoveTarget(hostile));
        }

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is update on the threat list.
        /// </summary>
        public virtual void OnThreatChange(IHostileEntity hostile)
        {
            scriptCollection?.Invoke<IUnitScript>(s => s.OnThreatChange(hostile));
        }

        private void UpdateCombatState()
        {
            // ensure conditions for combat state change are met
            if (ThreatManager.IsThreatened == InCombat)
                return;

            InCombat   = ThreatManager.IsThreatened;
            Sheathed   = !inCombat;
            StandState = inCombat ? StandState.Stand : StandState.State0;
        }
    }
}
