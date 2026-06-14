using NexusForever.Network.World.Message.Model.Shared;
using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Network.World.Message.Static;
using Microsoft.Extensions.DependencyInjection;
using NexusForever.Network.World.Message.Model;
using NexusForever.Script.Template.Collection;
using NexusForever.Game.Abstract.Spell.Event;
using NexusForever.Network.World.Combat;
using NexusForever.Network.World.Entity;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Spell;
using NexusForever.Game.Prerequisite;
using NexusForever.Game.Spell.Event;
using NexusForever.GameTable.Model;
using NexusForever.Script;
using NexusForever.Shared;
using System.Numerics;
using NLog;

namespace NexusForever.Game.Spell
{
    public partial class Spell : ISpell
    {
        // Invisible Unit for Field Spell Visuals
        private const uint InvisibleCreatureId = 16588; // 10242, 5609
        private const uint InvisibleDisplayId = 24307; // 28893
        
        private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public ISpellParameters Parameters { get; }
        public uint CastingId { get; }
        public bool IsCasting => status == SpellStatus.Casting;
        public bool IsExecuting => status is SpellStatus.Executing or SpellStatus.Channeled;
        public bool IsFinished => status == SpellStatus.Finished;
        public bool IsChanneled => Parameters.SpellInfo.BaseInfo.Entry.CastMethod == (uint)CastMethod.Channeled;

        public IUnitEntity Caster { get; }

        private SpellStatus status;

        private readonly List<ISpellTargetInfo> targets = new();
        private readonly List<ITelegraph> telegraphs = new();
        private readonly Dictionary<Vector3, INonPlayerEntity> activeAnchors = new();
        private bool anchorsConfigured;

        private readonly ISpellEventManager events = new SpellEventManager();

        private IScriptCollection scriptCollection;
        
        private double channelTimeRemaining;
        private int pendingBuffCount;

        public Spell(IUnitEntity caster, ISpellParameters parameters)
        {
            Caster     = caster;
            Parameters = parameters;
            CastingId  = GlobalSpellManager.Instance.NextCastingId;
            status     = SpellStatus.Initiating;

            parameters.RootSpellInfo ??= parameters.SpellInfo;

            scriptCollection = ScriptManager.Instance.InitialiseOwnedScripts<ISpell>(this, parameters.SpellInfo.Entry.Id);
        }
        
        private IUnitEntity GetEntityFromId(uint entityId) => Caster.Guid == entityId ? Caster : Caster.GetVisible<IUnitEntity>(entityId);
        
        public void Dispose()
        {
            
            if (scriptCollection == null) { return; }
            ScriptManager.Instance.Unload(scriptCollection);
            scriptCollection = null;
        }

        /// <summary>
        /// Enqueue a timed event on this spell's event queue.
        /// </summary>
        public void EnqueueEvent(ISpellEvent spellEvent)
        {
            events.EnqueueEvent(spellEvent);
        }

        /// <summary>
        /// Invoked by a timed <see cref="ISpellEvent"/> callback. Sets status to Finished
        /// if still executing and sends the SpellFinish packet to all visible entities.
        /// Used by handlers (e.g. <see cref="SpellEffectHandler.HandleFullScreenEffect"/>)
        /// that need to end the spell at a point external to the normal Update loop.
        /// </summary>
        public void OnTimedFinish()
        {
            if (status != SpellStatus.Executing) return;
            //Console.Error.WriteLine($"[SPELL] {CastingId} OnTimedFinish : Spell Status = {status}, Outstanding Buffs = {pendingBuffCount}");
            //Console.Error.WriteLine($"[SPELL] {CastingId} OnTimedFinish : Callback: {Environment.StackTrace}");
            SendSpellFinish();
        }

        /// <summary>
        /// Called by a buff's ExpireCallback when a tracked buff expires.
        /// Decrements the pending buff count and sends <see cref="ServerSpellFinish"/>
        /// when all buffs created by this spell have expired.
        /// </summary>
        private void OnBuffExpired(uint buffId)
        {
            //Console.Error.WriteLine($"[SPELL] {CastingId} BUFF EXPIRED : {buffId} : Spell Status = {status}, Outstanding Buffs = {pendingBuffCount - 1}");
            //Console.Error.WriteLine($"[SPELL] {CastingId} BUFF EXPIRED : Callback: {Environment.StackTrace}");
            
            if (--pendingBuffCount > 0)
                return;
            
            if (status == SpellStatus.Finished)
            {
                SendSpellFinish();
            }
        }

        public void Update(double lastTick)
        {
            scriptCollection.Invoke<IUpdate>(s => s.Update(lastTick));

            events.Update(lastTick);

            if (status == SpellStatus.Channeled)
            {
                channelTimeRemaining -= lastTick;
                if (channelTimeRemaining <= 0d)
                {
                    events.CancelEvents();
                    status = SpellStatus.Finished;
                    Log.Trace($"Spell {Parameters.SpellInfo.Entry.Id} channel has finished.");
                    SendSpellFinish();
                    return;
                }
            }

            if (status == SpellStatus.Executing && !events.HasPendingEvent && pendingBuffCount == 0)
            {
                status = SpellStatus.Finished;
                Log.Trace($"Spell {Parameters.SpellInfo.Entry.Id} has finished.");
                SendSpellFinish();
            }
        }

        /// <summary>
        /// Begin cast, checking prerequisites before initiating.
        /// </summary>
        public void Cast()
        {
            if (status != SpellStatus.Initiating)
                throw new InvalidOperationException();

            Log.Trace($"Spell {Parameters.SpellInfo.Entry.Id} has started initating.");

            if (Caster.IsStealthed && Parameters.SpellInfo.Entry.SpellCastStealthChange != 0)
            {
                Caster.RemoveStealth();
                Caster.EnqueueToVisible(new ServerCombatLog
                {
                    CombatLog = new CombatLogStealth
                    {
                        UnitId   = Caster.Guid,
                        BExiting = true
                    }
                }, true);

                foreach (IBuff stealthBuff in Caster.BuffManager.GetBuffs(b =>
                             b.EffectEntry.EffectType == SpellEffectType.Stealth).ToList())
                {
                    Caster.BuffManager.RemoveBuff(stealthBuff);
                }
            }

            CastResult result = CheckCast();
            if (result != CastResult.Ok)
            {
                SendSpellCastResult(result);
                return;
            }

            if (Caster is IPlayer player)
                if (Parameters.SpellInfo.GlobalCooldown != null)
                    player.SpellManager.SetGlobalSpellCooldown(Parameters.SpellInfo.Entry.GlobalCooldownEnum, Parameters.SpellInfo.GlobalCooldown.CooldownTime / 1000d);

            // It's assumed that non-player entities will be stood still to cast (most do). 
            // TODO: There are a handful of telegraphs that are attached to moving units (specifically rotating units) which this needs to be updated to account for.
            if (Caster is not IPlayer)
            {
                InitialiseTelegraphs();
            }

            SendSpellStart();

            // enqueue spell to be executed after cast time
            events.EnqueueEvent(new SpellEvent(Parameters.SpellInfo.Entry.CastTime / 1000d, Execute));
            status = SpellStatus.Casting;

            Log.Trace($"Spell {Parameters.SpellInfo.Entry.Id} has started casting.");
        }

        private CastResult CheckCast()
        {
            CastResult preReqCheck = CheckPrerequisites();
            if (preReqCheck != CastResult.Ok)
                return preReqCheck;

            CastResult ccResult = CheckCCConditions();
            if (ccResult != CastResult.Ok)
                return ccResult;

            if (Caster is IPlayer player)
            {
                if (player.SpellManager.GetSpellCooldown(Parameters.SpellInfo.Entry.Id) > 0d)
                    return CastResult.SpellCooldown;

                if (player.SpellManager.GetGlobalSpellCooldown(Parameters.SpellInfo.Entry.GlobalCooldownEnum) > 0d)
                    return CastResult.SpellGlobalCooldown;

                if (Parameters.CharacterSpell?.MaxAbilityCharges > 0 && Parameters.CharacterSpell?.AbilityCharges == 0)
                    return CastResult.SpellNoCharges;
                
                //Console.Error.WriteLine($"[Spell] {Parameters.SpellInfo.Entry.Id} : {Parameters.SpellInfo.Entry.Description} CheckCast ccResult -> {ccResult}");
            }

            if (Parameters.HasPrimaryTarget())
            {
                IUnitEntity primaryTarget = GetEntityFromId(Parameters.GetPrimaryTargetId());
                if (primaryTarget != null && primaryTarget.IsSpellImmune(Parameters.SpellInfo.BaseInfo.Entry.Id))
                    return CastResult.TargetImmuneToSpell;
            }

            CastResult vitalResult = CheckVitalCost();
            if (vitalResult != CastResult.Ok)
                return vitalResult;

            return CastResult.Ok;
        }

        private CastResult CheckPrerequisites()
        {
            if (Parameters.SpellInfo.CasterCastPrerequisite != null && !CheckRunnerOverride(Caster))
            {
                // Evaluate CasterCastPrerequisite for both players and NPCs against the game table PrerequisiteEntry.
                if (!PrerequisiteManager.Instance.Meets(Caster, Parameters.SpellInfo.CasterCastPrerequisite.Id))
                {
                    return CastResult.PrereqCasterCast;
                }
            }

            if (Parameters.SpellInfo.TargetCastPrerequisites != null)
            {
            }

            if (Parameters.SpellInfo.CasterPersistencePrerequisites != null)
            {
            }

            if (Parameters.SpellInfo.TargetPersistencePrerequisites != null)
            {
            }

            return CastResult.Ok;
        }

        private bool CheckRunnerOverride(IUnitEntity entity) => Parameters.SpellInfo.PrerequisiteRunners.Any(runnerPrereq => PrerequisiteManager.Instance.Meets(entity, runnerPrereq.Id));

        private CastResult CheckCCConditions()
        {
            CCState[] preventingCCStates =
            {
                CCState.Stun,
                CCState.Sleep,
                CCState.Fear,
                CCState.Hold,
                CCState.Polymorph,
                CCState.Disorient,
                CCState.Disable,
                CCState.Knockdown,
                CCState.Knockback,
                CCState.Silence,
                CCState.Disarm,
                CCState.Blind,
                CCState.Grounded,
                CCState.DisableCinematic,
                CCState.AbilityRestriction
            };

            foreach (CCState ccState in preventingCCStates)
            {
                if (Caster.HasCCState(ccState))
                    return GetPreventingCastResultForCCState(ccState);
            }

            if (Parameters.SpellInfo.CasterCCConditions != null)
            {
                Spell4CCConditionsEntry ccConditions = Parameters.SpellInfo.CasterCCConditions;
                if (ccConditions.CcStateFlagsRequired != 0u)
                {
                    CCState ccStateMask = (CCState)ccConditions.CcStateMask;
                    if (!Caster.HasCCState(ccStateMask))
                        return GetCastResultForCCState(ccStateMask);
                }
            }

            if (Parameters.SpellInfo.TargetCCConditions != null && Parameters.HasPrimaryTarget())
            {
                Spell4CCConditionsEntry ccConditions = Parameters.SpellInfo.TargetCCConditions;
                if (ccConditions.CcStateFlagsRequired != 0u)
                {
                    CCState ccStateMask = (CCState)ccConditions.CcStateMask;
                    IUnitEntity target = GetEntityFromId(Parameters.GetPrimaryTargetId());
                    if (target != null && !target.HasCCState(ccStateMask))
                        return GetCastResultForCCState(ccStateMask);
                }
            }

            return CastResult.Ok;
        }

        private static CastResult GetCastResultForCCState(CCState state)
        {
            return state switch
            {
                CCState.Stun                 => CastResult.CCStun,
                CCState.Sleep                => CastResult.CCSleep,
                CCState.Root                 => CastResult.CCRoot,
                CCState.Disarm               => CastResult.CCDisarm,
                CCState.Silence              => CastResult.CCSilence,
                CCState.Polymorph            => CastResult.CCPolymorph,
                CCState.Fear                 => CastResult.CCFear,
                CCState.Hold                 => CastResult.CCHold,
                CCState.Knockdown            => CastResult.CCKnockdown,
                CCState.Vulnerability        => CastResult.CCVulnerability,
                CCState.VulnerabilityWithAct => CastResult.CCVulnerabilityWithAct,
                CCState.Disorient            => CastResult.CCDisorient,
                CCState.Disable              => CastResult.CCDisable,
                CCState.Taunt                => CastResult.CCTaunt,
                CCState.DeTaunt              => CastResult.CCDeTaunt,
                CCState.Blind                => CastResult.CCBlind,
                CCState.Knockback            => CastResult.CCKnockback,
                CCState.Pushback             => CastResult.CCPushback,
                CCState.Pull                 => CastResult.CCPull,
                CCState.PositionSwitch       => CastResult.CCPositionSwitch,
                CCState.Tether               => CastResult.CCTether,
                CCState.Snare                => CastResult.CCSnare,
                CCState.Interrupt            => CastResult.CCInterrupt,
                CCState.Daze                 => CastResult.CCDaze,
                CCState.Subdue               => CastResult.CCSubdue,
                CCState.Grounded             => CastResult.CCGrounded,
                CCState.DisableCinematic     => CastResult.CCDisableCinematic,
                CCState.AbilityRestriction   => CastResult.CCAbilityRestriction,
                _                            => CastResult.Ok
            };
        }

        private static CastResult GetPreventingCastResultForCCState(CCState state)
        {
            return state switch
            {
                CCState.Stun                 => CastResult.CasterCannotBeStun,
                CCState.Sleep                => CastResult.CasterCannotBeSleep,
                CCState.Fear                 => CastResult.CasterCannotBeFear,
                CCState.Hold                 => CastResult.CasterCannotBeHold,
                CCState.Polymorph            => CastResult.CasterCannotBePolymorph,
                CCState.Disorient            => CastResult.CasterCannotBeDisorient,
                CCState.Disable              => CastResult.CasterCannotBeDisable,
                CCState.Knockdown            => CastResult.CasterCannotBeKnockdown,
                CCState.Knockback            => CastResult.CasterCannotBeKnockback,
                CCState.Silence              => CastResult.CasterCannotBeSilence,
                CCState.Disarm               => CastResult.CasterCannotBeDisarm,
                CCState.Blind                => CastResult.CasterCannotBeBlind,
                CCState.Grounded             => CastResult.CasterCannotBeGrounded,
                CCState.DisableCinematic     => CastResult.CasterCannotBeDisableCinematic,
                CCState.AbilityRestriction   => CastResult.CasterCannotBeAbilityRestriction,
                _                            => CastResult.Ok
            };
        }

        private CastResult CheckVitalCost()
        {
            Spell4Entry entry = Parameters.SpellInfo.Entry;

            if (entry.InnateCostType0 != 0u && entry.InnateCost0 != 0u)
            {
                Vital vitalType = (Vital)entry.InnateCostType0;
                float cost = entry.InnateCost0;
                if (vitalType == Vital.Focus)
                    cost *= Caster.GetProperty(Property.FocusCostModifier)?.Value ?? 1f;
                if (Caster.GetVitalValue(vitalType) < cost)
                    return GetCastResultForVitalCost(vitalType);
            }

            if (entry.InnateCostType1 != 0u && entry.InnateCost1 != 0u)
            {
                Vital vitalType = (Vital)entry.InnateCostType1;
                float cost = entry.InnateCost1;
                if (vitalType == Vital.Focus)
                    cost *= Caster.GetProperty(Property.FocusCostModifier)?.Value ?? 1f;
                if (Caster.GetVitalValue(vitalType) < cost)
                    return GetCastResultForVitalCost(vitalType);
            }

            return CastResult.Ok;
        }

        private static CastResult GetCastResultForVitalCost(Vital vital)
        {
            return vital switch
            {
                Vital.Health    => CastResult.CasterVitalCostHealth,
                Vital.Focus     => CastResult.CasterVitalCostFocus,
                Vital.Resource0 => CastResult.CasterVitalCostResource0,
                Vital.Resource1 => CastResult.CasterVitalCostResource1,
                Vital.Resource2 => CastResult.CasterVitalCostResource2,
                Vital.Resource3 => CastResult.CasterVitalCostResource3,
                Vital.Resource4 => CastResult.CasterVitalCostResource4,
                Vital.Resource5 => CastResult.CasterVitalCostResource5,
                Vital.Resource6 => CastResult.CasterVitalCostResource6,
                _               => CastResult.CasterVitalCost
            };
        }

        private float ResolveCastYaw() => Parameters.Yaw ?? Caster.Rotation.X;
        
        private void InitialiseTelegraphs()
        {
            telegraphs.Clear();

            //target locations for field spells
            if (!anchorsConfigured)
            {
                anchorsConfigured = true;
                
                if (Parameters.HasTelegraphPositions())
                {
                    foreach (Position position in Parameters.TelegraphPositions)
                    {
                        activeAnchors.Add(position.Vector, CreateAnchorEntity(position.Vector));
                    }
                }
            }
            
            foreach (TelegraphDamageEntry telegraphDamageEntry in Parameters.SpellInfo.Telegraphs.Where(t=> t.DisplayFlags != 0)) // should we do this?
            {
                telegraphs.Add(new Telegraph(telegraphDamageEntry, Caster, ResolveCastPosition(), new Vector3(ResolveCastYaw(), 0f, 0f)));
            }
        }
        
        private bool HasTelegraphPositions() => Parameters.HasTelegraphPositions();
        
        private Vector3 ResolveCastPosition()
        {
            if (HasTelegraphPositions())
            {
                return Parameters.GetDefaultTelegraphPosition();
            }

            if (Parameters.HasAttachedUnit())
            {
                return GetEntityFromId(Parameters.GetAttachedUnitId()).Position;
            }

            if (Parameters.HasPrimaryTarget())
            {
                return GetEntityFromId(Parameters.GetPrimaryTargetId()).Position;
            }
            
            return Caster.Position;
        }

        private uint ResolveTargetGuidFromFlags(uint fallbackGuid)
        {
            switch (ResolveVisualTargetType())
            {
                case SpellEffectTargetFlags.Caster:
                    return Caster.Guid;
                case SpellEffectTargetFlags.Target:
                case SpellEffectTargetFlags.Caster | SpellEffectTargetFlags.Target:
                    if (Parameters.HasAttachedUnit())
                    {
                        return Parameters.GetAttachedUnitId();
                    }
                    
                    if (Parameters.HasPrimaryTarget())
                    {
                        return Parameters.GetPrimaryTargetId();
                    }
                    break;
                case SpellEffectTargetFlags.None:
                case SpellEffectTargetFlags.Telegraph:
                    if (Parameters.HasTelegraphPositions())
                    {
                        return GetAnchorEntity(Parameters.GetDefaultTelegraphPosition()).Guid;
                    }
                    break;
                    
            }
            
            return fallbackGuid;
        }

        private uint ResolveTelegraphAttachment()
        {
            if (Parameters.HasTelegraphPositions())
            {
                return GetAnchorEntity(Parameters.GetDefaultTelegraphPosition()).Guid;
            }
            
            if (Parameters.HasAttachedUnit())
            {
                return Parameters.GetAttachedUnitId();
            }
            
            if (Parameters.HasPrimaryTarget())
            {
                return Parameters.GetPrimaryTargetId();
            }

            return Caster.Guid;
        }

        private INonPlayerEntity CreateAnchorEntity(Vector3 position)
        {
            INonPlayerEntity anchorEntity = LegacyServiceProvider.Provider.GetRequiredService<INonPlayerEntity>();
            anchorEntity.Initialise(InvisibleCreatureId);
            //anchorEntity.DisplayInfo = InvisibleDisplayId;
            anchorEntity.MovementManager.SetScale(0);
            anchorEntity.SetFaction(Caster.Faction2);
            anchorEntity.ModifyHealth(anchorEntity.MaxHealth, DamageType.Heal, anchorEntity);
            Caster.Map.ForceAddImmediate(anchorEntity, position);
            return anchorEntity;
        }
        
        private INonPlayerEntity GetAnchorEntity(Vector3 position)
        {
            INonPlayerEntity anchorEntity = activeAnchors.GetValueOrDefault(position);
            if (anchorEntity == null)
            {
                Log.Error($"SPELL {CastingId} Anchor Lookup For Position {position} Failed!");
            }
            return anchorEntity;
        }

        private uint GetTelegraphAttachedUnitId(ITelegraph telegraph)
        {
            SpellEffectTargetFlags telegraphFlags = (SpellEffectTargetFlags) telegraph.TelegraphDamage.TargetTypeFlags;

            if (telegraphFlags.HasFlag(SpellEffectTargetFlags.Caster))
                return Caster.Guid;

            if (telegraphFlags.HasFlag(SpellEffectTargetFlags.Target))
            {
                if (Parameters.HasAttachedUnit())
                    return Parameters.GetAttachedUnitId();
                if (Parameters.HasPrimaryTarget())
                    return Parameters.GetPrimaryTargetId();
            }

            return Caster.Guid;
        }

        private SpellEffectTargetFlags ResolveVisualTargetType()
        {
            SpellEffectTargetFlags flags = 0;
            foreach (ITelegraph telegraph in telegraphs)
                flags |= (SpellEffectTargetFlags)telegraph.TelegraphDamage.TargetTypeFlags;

            if (HasTelegraphPositions() && flags == 0)
                return SpellEffectTargetFlags.Telegraph;

            if (flags != 0)
                return flags;

            if (Parameters.HasAttachedUnit() || Parameters.HasPrimaryTarget())
                return SpellEffectTargetFlags.Target;

            return SpellEffectTargetFlags.Caster;
        }

        private IEnumerable<IWorldEntity> ResolveVisualUnits()
        {
            SpellEffectTargetFlags targetType = ResolveVisualTargetType();

            if (HasTelegraphPositions())
            {
                IWorldEntity anchor = GetAnchorEntity(Parameters.GetDefaultTelegraphPosition());
                if (anchor != null)
                    yield return anchor;
            }

            // Caster|Target: emit target first, then caster
            if (targetType.HasFlag(SpellEffectTargetFlags.Target))
            {
                IUnitEntity primary = null;
                if (Parameters.HasAttachedUnit())
                {
                    primary = GetEntityFromId(Parameters.GetAttachedUnitId());
                }
                else if (Parameters.HasPrimaryTarget())
                {
                    primary = GetEntityFromId(Parameters.GetPrimaryTargetId());
                }
                
                if (primary != null)
                {
                    yield return primary;
                }
            }

            // Caster: emit caster last
            if (targetType.HasFlag(SpellEffectTargetFlags.Caster))
                yield return Caster;
        }

        /// <summary>
        /// Cancel cast with supplied <see cref="CastResult"/>.
        /// </summary>
        public void CancelCast(CastResult result)
        {
            if (status != SpellStatus.Casting && status != SpellStatus.Channeled)
                throw new InvalidOperationException();

            if (Caster is IPlayer { IsLoading: false } player)
            {
                player.Session.EnqueueMessageEncrypted(new Server07F9
                {
                    ServerUniqueId = CastingId,
                    CastResult     = result,
                    CancelCast     = true
                });
            }

            events.CancelEvents();
            status = SpellStatus.Finished;

            Log.Trace($"Spell {Parameters.SpellInfo.Entry.Id} cast was cancelled.");
        }

        private void Execute()
        {
            status = SpellStatus.Executing;
            Log.Trace($"Spell {Parameters.SpellInfo.Entry.Id} has started executing.");

            if (Caster is IPlayer player)
                if (Parameters.SpellInfo.Entry.SpellCoolDown != 0u)
                    player.SpellManager.SetSpellCooldown(Parameters.SpellInfo.Entry.Id, Parameters.SpellInfo.Entry.SpellCoolDown / 1000d);

            SelectTargets();
            ExecuteEffects();
            CostSpell();

            if (Caster is IPlayer questPlayer)
            {
                questPlayer.QuestManager.ObjectiveUpdate(QuestObjectiveType.SpellSuccess,  Parameters.SpellInfo.Entry.Id, 1u);
                questPlayer.QuestManager.ObjectiveUpdate(QuestObjectiveType.SpellSuccess2, Parameters.SpellInfo.Entry.Id, 1u);
                questPlayer.QuestManager.ObjectiveUpdate(QuestObjectiveType.SpellSuccess3, Parameters.SpellInfo.Entry.Id, 1u);
                questPlayer.QuestManager.ObjectiveUpdate(QuestObjectiveType.SpellSuccess4, Parameters.SpellInfo.Entry.Id, 1u);
            }

            SendSpellGo();

            // For non-channeled executing spells, schedule SendSpellFinish so the client receives
            // the finish packet after the spell completes rather than immediately on the next tick.
            // Channeled spells handle their own SpellFinish via ChannelPulse and ChannelMaxTime.
            if (!IsChanneled)
            {
                double finishDelay = Math.Max(Parameters.SpellInfo.Entry.CastTime, Parameters.SpellInfo.Entry.SpellDuration) / 1000d;
                if (finishDelay <= 0d)
                    finishDelay = 0.1d; // minimum fire-and-forget delay

                events.EnqueueEvent(new SpellEvent(finishDelay, OnTimedFinish));
            }

            if (!IsChanneled || Parameters.SpellInfo.Entry.ChannelMaxTime <= 0u) { return; }
            
            status = SpellStatus.Channeled;
            channelTimeRemaining = Parameters.SpellInfo.Entry.ChannelMaxTime / 1000d;

            double pulseInterval = Parameters.SpellInfo.Entry.ChannelPulseTime / 1000d;
            double initialDelay = Parameters.SpellInfo.Entry.ChannelInitialDelay / 1000d;

            double channelDuration = channelTimeRemaining;
            double nextPulse = initialDelay > 0d ? initialDelay : pulseInterval;

            while (nextPulse <= channelDuration)
            {
                double pulseTime = nextPulse;
                events.EnqueueEvent(new SpellEvent(pulseTime, ChannelPulse));
                nextPulse += pulseInterval;
            }

            Log.Trace($"Spell {Parameters.SpellInfo.Entry.Id} has started channeling for {channelDuration}s, pulsing every {pulseInterval}s.");
        }

        private void ChannelPulse()
        {
            targets.Clear();
            SelectTargets();
            ExecuteEffects();

            if (Caster is IPlayer player)
            {
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.SpellSuccess,  Parameters.SpellInfo.Entry.Id, 1u);
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.SpellSuccess2, Parameters.SpellInfo.Entry.Id, 1u);
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.SpellSuccess3, Parameters.SpellInfo.Entry.Id, 1u);
                player.QuestManager.ObjectiveUpdate(QuestObjectiveType.SpellSuccess4, Parameters.SpellInfo.Entry.Id, 1u);
            }

            SendSpellGo();
        }

        private void CostSpell()
        {
            if (Parameters.CharacterSpell?.MaxAbilityCharges > 0)
                Parameters.CharacterSpell.UseCharge();

            Spell4Entry entry = Parameters.SpellInfo.Entry;

            if (entry.InnateCostType0 != 0u && entry.InnateCost0 != 0u)
            {
                Vital vitalType = (Vital)entry.InnateCostType0;
                float cost = entry.InnateCost0;
                if (vitalType == Vital.Focus)
                    cost *= Caster.GetProperty(Property.FocusCostModifier)?.Value ?? 1f;
                Caster.ModifyVital(vitalType, -cost);
            }

            if (entry.InnateCostType1 != 0u && entry.InnateCost1 != 0u)
            {
                Vital vitalType = (Vital)entry.InnateCostType1;
                float cost = entry.InnateCost1;
                if (vitalType == Vital.Focus)
                    cost *= Caster.GetProperty(Property.FocusCostModifier)?.Value ?? 1f;
                Caster.ModifyVital(vitalType, -cost);
            }
        }

        private void SelectTargets()
        {
            targets.Add(new SpellTargetInfo(SpellEffectTargetFlags.Caster, Caster));

            if (Parameters.HasPrimaryTarget())
            {
                IUnitEntity primaryTargetEntity = GetEntityFromId(Parameters.GetPrimaryTargetId());
                if (primaryTargetEntity != null)
                    targets.Add(new SpellTargetInfo(SpellEffectTargetFlags.Target, primaryTargetEntity));
            }

            if (Caster is IPlayer)
                InitialiseTelegraphs();

            HashSet<uint> anchorGuids = activeAnchors.Count > 0
                ? [..activeAnchors.Values.Select(a => a.Guid)]
                : null;
            
            HashSet<uint> telegraphedEntities = new();

            foreach (ITelegraph telegraph in telegraphs)
            {
                foreach (IUnitEntity entity in telegraph.GetTargets())
                {
                    // what if this is a positive spell?
                    if (entity.CreatureId == Caster.CreatureId) continue;
                    // same... rez?
                    if (!entity.IsAlive) continue;
                    
                    // ignore anchor entities
                    if (anchorGuids?.Contains(entity.Guid) == true) continue;
                    
                    // spell has multiple telegraphs but only one is calculated... why?
                    if (!telegraphedEntities.Add(entity.Guid)) continue;
                    targets.Add(new SpellTargetInfo(SpellEffectTargetFlags.Telegraph, entity));
                }
            }
        }

        private void ExecuteEffects()
        {
            uint effectIndex = 0;
            const int maxLoopIterations = 10000;
            int loopCounter = 0;
            
            //Console.Error.WriteLine($"[SPELL] {CastingId} EXECUTE : {Parameters.SpellInfo.Entry.Id} : {Parameters.SpellInfo.Entry.Description} -> targets={targets.Count}"); 

            foreach (Spell4EffectsEntry spell4EffectsEntry in Parameters.SpellInfo.Effects)
            {
                //Console.Error.WriteLine($"[SPELL] {CastingId} EXECUTE : {Parameters.SpellInfo.Entry.Id} : Checking EffectEntry {spell4EffectsEntry.Id}"); 
                
                loopCounter++;
                if (loopCounter > maxLoopIterations)
                {
                    Log.Error($"Spell {Parameters.SpellInfo.Entry.Id} exceeded max loop iterations in ExecuteEffects, aborting to prevent infinite loop.");
                    break;
                }

                if (Caster.IsSpellEffectSuppressed(Parameters.SpellInfo.BaseInfo.Entry.Id, effectIndex))
                {
                    //Console.Error.WriteLine($"[SPELL] {CastingId} EXECUTE : {Parameters.SpellInfo.Entry.Id} : {Parameters.SpellInfo.BaseInfo.Entry.Id} Suppressed"); 
                    effectIndex++;
                    continue;
                }

                // select targets for effect
                List<ISpellTargetInfo> effectTargets = targets
                    .Where(t => (t.Flags & (SpellEffectTargetFlags)spell4EffectsEntry.TargetFlags) != 0)
                    .ToList();

                SpellEffectDelegate handler = GlobalSpellManager.Instance.GetEffectHandler(spell4EffectsEntry.EffectType);
                if (handler == null)
                {
                    Log.Warn($"Unhandled spell effect {spell4EffectsEntry.EffectType}");
                }
                else
                {
                    uint effectId = GlobalSpellManager.Instance.NextEffectId;
                    foreach (SpellTargetInfo effectTarget in effectTargets)
                    {
                        //Console.Error.WriteLine($"[SPELL] {CastingId} EXECUTE : {Parameters.SpellInfo.Entry.Id} : Checking Effect {spell4EffectsEntry.EffectType} for Entity {effectTarget.Entity.Guid}"); 
                        try
                        {
                            if (effectTarget.Entity.IsSpellEffectImmune(spell4EffectsEntry.EffectType))
                                continue;

                            SpellTargetInfo.SpellTargetEffectInfo info = new SpellTargetInfo.SpellTargetEffectInfo(effectId, spell4EffectsEntry);
                            effectTarget.Effects.Add(info);
                            
                            //Console.Error.WriteLine($"[SPELL] {CastingId} EXECUTE : {Parameters.SpellInfo.Entry.Id} : Effect {spell4EffectsEntry.EffectType} Added to Entity {effectTarget.Entity.Guid}"); 

                            handler.Invoke(this, effectTarget.Entity, info);

                            IBuff buff = effectTarget.Entity.BuffManager.GetBuffByEffectId(info.EffectId);

                            if (buff == null || (!(buff.Duration > 0d) && !buff.HasIcon) || buff.IsChanneled) continue;
                            
                            pendingBuffCount++;
                            Action<IBuff> originalCallback = buff.ExpireCallback;
                            buff.ExpireCallback = b =>
                            {
                                originalCallback?.Invoke(b);
                                OnBuffExpired(b.EffectId);
                            };

                            //Console.Error.WriteLine($"[SPELL] {CastingId} EXECUTE : {Parameters.SpellInfo.Entry.Id} : Buff Configured {spell4EffectsEntry.EffectType} Created For {effectTarget.Entity.Guid} Time: {buff.Duration}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Unhandled exception in spell effect handler {spell4EffectsEntry.EffectType} for spell {Parameters.SpellInfo.Entry.Id} on target {effectTarget.Entity.Guid}, skipping target.");
                        }
                    }
                }

                effectIndex++;
            }
        }

        public bool IsMovingInterrupted()
        {
            if (IsChanneled && status == SpellStatus.Channeled)
                return true;

            return Parameters.SpellInfo.Entry.CastTime > 0;
        }

        private void SendSpellCastResult(CastResult castResult)
        {
            if (castResult == CastResult.Ok)
                return;

            Log.Trace($"Spell {Parameters.SpellInfo.Entry.Id} failed to cast {castResult}.");

            if (Caster is IPlayer { IsLoading: false } player)
            {
                player.Session.EnqueueMessageEncrypted(new ServerSpellCastResult
                {
                    Spell4Id   = Parameters.SpellInfo.Entry.Id,
                    CastResult = castResult
                });
            }
        }

        private void SendSpellStart()
        {
            SpellEffectTargetFlags unitFlags = ResolveVisualTargetType();
            
            ServerSpellStart spellStart = new ServerSpellStart
            {
                CastingId              = CastingId,
                CasterId               = HasTelegraphPositions() && (unitFlags is SpellEffectTargetFlags.Telegraph or SpellEffectTargetFlags.None) ? ResolveTargetGuidFromFlags(Caster.Guid) : Caster.Guid,
                PrimaryTargetId        = ResolveTargetGuidFromFlags(0),
                Spell4Id               = Parameters.SpellInfo.Entry.Id,
                RootSpell4Id           = Parameters.RootSpellInfo?.Entry.Id ?? 0,
                ParentSpell4Id         = Parameters.ParentSpellInfo?.Entry.Id ?? 0,
                FieldPosition          = new Position(ResolveCastPosition()),
                Yaw                    = ResolveCastYaw(),
                UserInitiatedSpellCast = Parameters.UserInitiatedSpellCast,
                InitialPositionData    = new List<ServerSpellStart.InitialPosition>(),
                TelegraphPositionData  = new List<ServerSpellStart.TelegraphPosition>()
            };
            
            List<IWorldEntity> unitsVisuals = ResolveVisualUnits().ToList();
            
            foreach (IWorldEntity unit in unitsVisuals)
            {
                spellStart.InitialPositionData.Add(new ServerSpellStart.InitialPosition
                {
                    UnitId = unit.Guid,
                    Position = new Position(unit.Position),
                    TargetFlags = (byte) unitFlags,
                    Yaw = unit.Rotation.X
                });
            }
            
            foreach (ITelegraph telegraph in telegraphs)
            {
                spellStart.TelegraphPositionData.Add(new ServerSpellStart.TelegraphPosition
                {
                    TelegraphId = (ushort)telegraph.TelegraphDamage.Id,
                    AttachedUnitId = GetTelegraphAttachedUnitId(telegraph),
                    TargetFlags = (byte) unitFlags, //telegraph.TelegraphDamage.TargetTypeFlags,
                    Position = new Position(ResolveCastPosition()),
                    Yaw = telegraph.Rotation.X
                });
            }

            // Fallback: position overridden with game table telegraphs but no visual units emitted.
            // Emit TelegraphPositionData directly at world position with per-telegraph attachment.
            if (HasTelegraphPositions() && unitsVisuals.Count == 0 && telegraphs.Count > 0)
            {
                foreach (ITelegraph telegraph in telegraphs)
                {
                    spellStart.TelegraphPositionData.Add(new ServerSpellStart.TelegraphPosition
                    {
                        TelegraphId    = (ushort)telegraph.TelegraphDamage.Id,
                        AttachedUnitId = GetTelegraphAttachedUnitId(telegraph),
                        TargetFlags    = (byte)telegraph.TelegraphDamage.TargetTypeFlags,
                        Position       = new Position(ResolveCastPosition()),
                        Yaw            = telegraph.Rotation.X
                    });
                }
            }

            // Fallback: when position is overridden but no game table telegraphs exist,
            // emit a single entry so the client knows where to render spell effects.
            if (HasTelegraphPositions() && unitsVisuals.Count == 0 && telegraphs.Count == 0)
            {
                spellStart.InitialPositionData.Add(new ServerSpellStart.InitialPosition
                {
                    UnitId      = Caster.Guid,
                    Position    = new Position(ResolveCastPosition()),
                    TargetFlags = (byte) unitFlags,
                    Yaw         = ResolveCastYaw()
                });
            }

            Caster.EnqueueToVisible(spellStart, true);
        }

        private void SendSpellGo()
        {
            List<ICombatLog> combatLogs = [];

            var serverSpellGo = new ServerSpellGo
            {
                ServerUniqueId     = CastingId,
                PrimaryDestination = new Position(ResolveCastPosition()),
                Phase              = -1
            };

            List<bool> targetInfoHasRavel = new();
            List<bool> targetIsCaster = new();

            foreach (ISpellTargetInfo targetInfo in targets.Where(t => t.Effects.Count > 0))
            {
                if (targetInfo.Effects.All(x => x.DropEffect))
                {
                    combatLogs.AddRange(targetInfo.Effects.SelectMany(i => i.CombatLogs));
                    continue;
                }

                TargetInfo networkTargetInfo = new TargetInfo
                {
                    UnitId        = targetInfo.Entity.Guid,
                    TargetFlags   = 1,
                    InstanceCount = 1,
                    CombatResult  = CombatResult.Hit
                };

                bool hasRavel = false;

                foreach (ISpellTargetEffectInfo targetEffectInfo in targetInfo.Effects)
                {
                    if (targetEffectInfo.DropEffect)
                    {
                        combatLogs.AddRange(targetEffectInfo.CombatLogs);
                        continue;
                    }

                    switch (targetEffectInfo.Entry.EffectType)
                    {
                        case SpellEffectType.Proxy:
                            continue;
                        case SpellEffectType.RavelSignal:
                            hasRavel = true;
                            break;
                    }

                    TargetInfo.EffectInfo networkTargetEffectInfo = new TargetInfo.EffectInfo
                    {
                        Spell4EffectId = targetEffectInfo.Entry.Id,
                        EffectUniqueId = targetEffectInfo.EffectId,
                    };

                    double effectDuration = targetEffectInfo.Entry.DurationTime > 0
                        ? targetEffectInfo.Entry.DurationTime / 1000d
                        : Parameters.SpellInfo.Entry.SpellDuration / 1000d;
                    networkTargetEffectInfo.TimeRemaining = effectDuration > 0d
                        ? (int)Math.Ceiling(effectDuration)
                        : -1;

                    if (targetEffectInfo.Damage != null)
                    {
                        networkTargetEffectInfo.InfoType = 1;
                        networkTargetEffectInfo.DamageDescriptionData = new TargetInfo.EffectInfo.DamageDescription
                        {
                            RawDamage          = targetEffectInfo.Damage.RawDamage,
                            RawScaledDamage    = targetEffectInfo.Damage.RawScaledDamage,
                            AbsorbedAmount     = targetEffectInfo.Damage.AbsorbedAmount,
                            ShieldAbsorbAmount = targetEffectInfo.Damage.ShieldAbsorbAmount,
                            AdjustedDamage     = targetEffectInfo.Damage.AdjustedDamage,
                            OverkillAmount     = targetEffectInfo.Damage.OverkillAmount,
                            KilledTarget       = targetEffectInfo.Damage.KilledTarget,
                            CombatResult       = targetEffectInfo.Damage.CombatResult,
                            DamageType         = targetEffectInfo.Damage.DamageType
                        };
                    }

                    networkTargetInfo.EffectInfoData.Add(networkTargetEffectInfo);
                    
                    combatLogs.AddRange(targetEffectInfo.CombatLogs);
                }

                serverSpellGo.TargetInfoData.Add(networkTargetInfo);
                targetInfoHasRavel.Add(hasRavel);
                targetIsCaster.Add((targetInfo.Flags & SpellEffectTargetFlags.Caster) != 0);
            }

            // Align caster ravel effect(s) to the same visual anchor as the telegraph.
            // The ravel is a client-side visual anchored to TargetInfo.UnitId;
            // redirecting only the entries that target the caster keeps damage/heal
            // feedback on the original target entity and avoids overwriting
            // already-correct unit IDs (e.g. mine entity at its own position).
            uint anchor = ResolveTelegraphAttachment();
            if (anchor != Caster.Guid)
            {
                for (int i = 0; i < serverSpellGo.TargetInfoData.Count; i++)
                {
                    if (targetInfoHasRavel[i] && targetIsCaster[i])
                        serverSpellGo.TargetInfoData[i].UnitId = anchor;
                }
            }
            
            byte telegraphTargetFlags = (byte) ResolveVisualTargetType();

            List<IWorldEntity> unitsVisuals = ResolveVisualUnits().ToList();

            foreach (IWorldEntity unit in unitsVisuals)
            {
                byte unitFlags = unit.Guid == Caster.Guid
                    ? (byte) SpellEffectTargetFlags.Caster
                    : telegraphTargetFlags;

                bool useAnchorOverride = HasTelegraphPositions() && ((SpellEffectTargetFlags)unitFlags is SpellEffectTargetFlags.Telegraph or SpellEffectTargetFlags.None);
                
                serverSpellGo.InitialPositionData.Add(new InitialPosition
                {
                    UnitId = useAnchorOverride ? ResolveTargetGuidFromFlags(Caster.Guid) : unit.Guid,
                    Position = useAnchorOverride ? new Position(ResolveCastPosition()) : new Position(unit.Position),
                    TargetFlags = unitFlags,
                    Yaw = unit.Rotation.X // wrong if we override w/ anchor
                });
            }

            foreach (ITelegraph telegraph in telegraphs)
            {
                serverSpellGo.TelegraphPositionData.Add(new TelegraphPosition
                {
                    TelegraphId = (ushort)telegraph.TelegraphDamage.Id,
                    AttachedUnitId = GetTelegraphAttachedUnitId(telegraph),
                    TargetFlags = telegraphTargetFlags,
                    Position = new Position(ResolveCastPosition()),
                    Yaw = telegraph.Rotation.X
                });
            }

            // Fallback: position overridden with game table telegraphs but no visual units emitted.
            if (HasTelegraphPositions() && unitsVisuals.Count == 0 && telegraphs.Count > 0)
            {
                foreach (ITelegraph telegraph in telegraphs)
                {
                    serverSpellGo.TelegraphPositionData.Add(new TelegraphPosition
                    {
                        TelegraphId    = (ushort)telegraph.TelegraphDamage.Id,
                        AttachedUnitId = GetTelegraphAttachedUnitId(telegraph),
                        TargetFlags    = telegraphTargetFlags,
                        Position       = new Position(ResolveCastPosition()),
                        Yaw            = telegraph.Rotation.X
                    });
                }
            }

            // Fallback: when position is overridden but no game table telegraphs exist.
            if (HasTelegraphPositions() && unitsVisuals.Count == 0 && telegraphs.Count == 0)
            {
                byte targetType = (byte) ResolveVisualTargetType();
                serverSpellGo.InitialPositionData.Add(new InitialPosition
                {
                    UnitId      = Caster.Guid,
                    Position    = new Position(ResolveCastPosition()),
                    TargetFlags = targetType,
                    Yaw         = ResolveCastYaw()
                });
            }

            foreach (ICombatLog combatLog in combatLogs)
            {
                Caster.EnqueueToVisible(new ServerCombatLog
                {
                    CombatLog = combatLog
                }, true);
            }

            Caster.EnqueueToVisible(serverSpellGo, true);
        }
        
        private void SendSpellFinish()
        {
            if (status != SpellStatus.Finished)
                return;
            
            if (anchorsConfigured)
            {
                anchorsConfigured = false;
                // is this too soon?
                foreach (KeyValuePair<Vector3, INonPlayerEntity> anchorEntity in activeAnchors)
                {
                    anchorEntity.Value.RemoveFromMap();
                    anchorEntity.Value.Dispose();
                }
            
                activeAnchors.Clear();
            }
            
            if(pendingBuffCount > 0) { return ;}
            
            //Console.Error.WriteLine($"[SPELL] {CastingId} SendSpellFinish : Callback: {Environment.StackTrace}");
            
            //TODO: responsible for prematurely removing buff icons and Ravel effects
            Caster.EnqueueToVisible(new ServerSpellFinish { ServerUniqueId = CastingId, }, true);
        }

        private void SendRemoveBuff(uint unitId)
        {
            if (!Parameters.SpellInfo.BaseInfo.HasIcon)
                throw new InvalidOperationException();

            Caster.EnqueueToVisible(new ServerSpellBuffRemove
            {
                CastingId = CastingId,
                CasterId  = unitId
            }, true);
        }
    }
}
