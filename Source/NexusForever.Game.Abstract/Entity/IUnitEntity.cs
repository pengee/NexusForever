using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;

namespace NexusForever.Game.Abstract.Entity
{
    /// <summary>
    /// An <see cref="IUnitEntity"/> is an extension to <see cref="IWorldEntity"/> which can cast spells, be targed by spells and participate in combat.
    /// </summary>
    public interface IUnitEntity : IWorldEntity
    {
        float HitRadius { get; }

        /// <summary>
        /// Guid of the <see cref="IWorldEntity"/> currently targeted.
        /// </summary>
        uint? TargetGuid { get; }

        /// <summary>
        /// Determines whether or not this <see cref="IUnitEntity"/> is alive.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Determines whether or not this <see cref="IUnitEntity"/> is in combat.
        /// </summary>
        bool InCombat { get; }

        public IThreatManager ThreatManager { get; }

        IBuffManager BuffManager { get; }

        IProcManager ProcManager { get; }

        uint Absorption { get; set; }
        uint HealingAbsorption { get; set; }

        uint InterruptArmorBase { get; }

        /// <summary>
        /// Add a <see cref="Property"/> modifier given a Spell4Id and <see cref="ISpellPropertyModifier"/> instance.
        /// </summary>
        void AddSpellModifierProperty(ISpellPropertyModifier modifier, uint spell4Id);

        /// <summary>
        /// Remove a <see cref="Property"/> modifier by a Spell that is currently affecting this <see cref="IUnitEntity"/>.
        /// </summary>
        void RemoveSpellProperty(Property property, uint spell4Id);

        /// <summary>
        /// Remove all <see cref="Property"/> modifiers by a Spell that is currently affecting this <see cref="IUnitEntity"/>
        /// </summary>
        void RemoveSpellProperties(uint spell4Id);

        /// <summary>
        /// Returns whether the <see cref="IUnitEntity"/> currently has the supplied <see cref="CCState"/> active.
        /// </summary>
        bool HasCCState(CCState state);

        bool HasMovementCC();

        bool HasSpellCC();
        
        /// <summary>
        /// Apply a <see cref="CCState"/> to this <see cref="IUnitEntity"/> for the supplied duration (in seconds).
        /// </summary>
        /// <remarks>
        /// The duration is tracked on the unit itself so the <see cref="CCState"/> is removed when the timer
        /// expires, independent of any associated buff.
        /// </remarks>
        void ApplyCCState(CCState state, uint castingId, uint effectId, IUnitEntity caster = null, double duration = 0d);

        /// <summary>
        /// Remove a <see cref="CCState"/> from this <see cref="IUnitEntity"/>.
        /// </summary>
        void RemoveCCState(CCState state, uint castingId, uint effectId);

        /// <summary>
        /// Remove all active <see cref="CCState"/> instances from this <see cref="IUnitEntity"/>.
        /// </summary>
        void RemoveAllCCStates();

        /// <summary>
        /// Check diminishing returns for the supplied DR category and return the apply result.
        /// </summary>
        CCStateApplyRulesResult CheckDiminishingReturns(ushort drCategoryId);

        /// <summary>
        /// Return the duration modifier for the supplied DR category based on current hit count.
        /// </summary>
        float GetDRDurationModifier(ushort drCategoryId);

        /// <summary>
        /// Record a DR application for the supplied category, incrementing the hit count and resetting the cooldown.
        /// </summary>
        void RecordDRApplication(ushort drCategoryId);

        /// <summary>
        /// Update diminishing returns timers, resetting expired DR states.
        /// </summary>
        void UpdateDR(double lastTick);

        /// <summary>
        /// Attempt to consume interrupt armor. Returns the number of interrupt armor points consumed.
        /// </summary>
        uint ConsumeInterruptArmor();

        /// <summary>
        /// Modify a <see cref="Vital"/> on this <see cref="IUnitEntity"/> by the supplied amount.
        /// </summary>
        void ModifyVital(Vital vital, float amount);

        /// <summary>
        /// Return the current value of the supplied <see cref="Vital"/>.
        /// </summary>
        float GetVitalValue(Vital vital);

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell id and <see cref="ISpellParameters"/>.
        /// </summary>
        void CastSpell(uint spell4Id, ISpellParameters parameters);

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell base id, tier and primary target id.
        /// </summary>
        void CastSpell(uint spell4BaseId, byte tier, uint primaryTargetId);

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied spell base id, tier and <see cref="ISpellParameters"/>.
        /// </summary>
        void CastSpell(uint spell4BaseId, byte tier, ISpellParameters parameters);

        /// <summary>
        /// Cast a <see cref="ISpell"/> with the supplied <see cref="ISpellParameters"/>.
        /// </summary>
        void CastSpell(ISpellParameters parameters);

        /// <summary>
        /// Cancel any <see cref="ISpell"/>'s that are interrupted by movement.
        /// </summary>
        void CancelSpellsOnMove();

        /// <summary>
        /// Cancel any active channeled <see cref="ISpell"/>'s on this <see cref="IUnitEntity"/>.
        /// </summary>
        void CancelChanneledSpells();

        /// <summary>
        /// Cancel an <see cref="ISpell"/> based on its casting id.
        /// </summary>
        /// <param name="castingId">Casting ID of the spell to cancel</param>
        void CancelSpellCast(uint castingId);

        /// <summary>
        /// Determine if this <see cref="IUnitEntity"/> can attack supplied <see cref="IUnitEntity"/>.
        /// </summary>
        bool CanAttack(IUnitEntity target);

        /// <summary>
        /// Returns whether or not this <see cref="IUnitEntity"/> is an attackable target.
        /// </summary>
        bool IsValidAttackTarget();

        /// <summary>
        /// Deal damage to this <see cref="IUnitEntity"/> from the supplied <see cref="IUnitEntity"/>.
        /// </summary>
        void TakeDamage(IUnitEntity attacker, IDamageDescription damageDescription);

        /// <summary>
        /// Modify the health of this <see cref="IUnitEntity"/> by the supplied amount.
        /// </summary>
        /// <remarks>
        /// If the <see cref="DamageType"/> is <see cref="DamageType.Heal"/> amount is added to current health otherwise subtracted.
        /// </remarks>
        void ModifyHealth(uint amount, DamageType type, IUnitEntity source);

        /// <summary>
        /// Set target to supplied target guid.
        /// </summary>
        /// <remarks>
        /// A null target will clear the current target.
        /// </remarks>
        void SetTarget(uint? target, uint threat = 0u);

        /// <summary>
        /// Set target to supplied <see cref="IUnitEntity"/>.
        /// </summary>
        /// <remarks>
        /// A null target will clear the current target.
        /// </remarks>
        void SetTarget(IWorldEntity target, uint threat = 0u);

        /// <summary>
        /// Invoked when a new <see cref="IHostileEntity"/> is added to the threat list.
        /// </summary>
        void OnThreatAddTarget(IHostileEntity hostile);

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is removed from the threat list.
        /// </summary>
        void OnThreatRemoveTarget(IHostileEntity hostile);

        /// <summary>
        /// Invoked when an existing <see cref="IHostileEntity"/> is update on the threat list.
        /// </summary>
        void OnThreatChange(IHostileEntity hostile);

        bool IsStealthed { get; }
        float StealthLevel { get; }

        void SetStealth(float level);
        void RemoveStealth();

        void SuppressSpellEffect(uint spell4BaseId, uint effectIndex);
        void UnsuppressSpellEffect(uint spell4BaseId, uint effectIndex);
        bool IsSpellEffectSuppressed(uint spell4BaseId, uint effectIndex);

        bool AggroImmune { get; }
        void SetAggroImmune(bool immune);
        void AddSpellImmunity(uint spell4BaseId);
        void RemoveSpellImmunity(uint spell4BaseId);
        bool IsSpellImmune(uint spell4BaseId);
        void AddSpellEffectImmunity(SpellEffectType effectType);
        void RemoveSpellEffectImmunity(SpellEffectType effectType);
        bool IsSpellEffectImmune(SpellEffectType effectType);

        bool DelayDeath { get; }
        void SetDelayDeath(bool delayDeath);
    }
}
