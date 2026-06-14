using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract;
using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Spell;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Combat;

namespace NexusForever.Game.Combat
{
    public sealed class DamageCalculator : IDamageCalculator
    {
        #region Dependency Injection

        private readonly ILogger<DamageCalculator> log;
        private readonly IGameTableManager gameTableManager;

        public DamageCalculator(
            ILogger<DamageCalculator> log,
            IGameTableManager gameTableManager)
        {
            this.log              = log;
            this.gameTableManager = gameTableManager;
        }

        #endregion

        /// <summary>
        /// Returns the calculated damage and updates the referenced <see cref="SpellTargetInfo.SpellTargetEffectInfo"/> appropriately.
        /// </summary>
        /// <remarks>
        /// TODO: This should probably return an instance of a Class which describes all the damage done to both entities. Attackers can have reflected damage from this, etc.
        /// </remarks>
        public void CalculateDamage(IUnitEntity attacker, IUnitEntity victim, ISpell spell, ISpellTargetEffectInfo info)
        {
            IDamageDescription damageDescription = new SpellTargetInfo.SpellTargetEffectInfo.DamageDescription
            {
                DamageType   = info.Entry.DamageType,
                CombatResult = CombatResult.Hit
            };

            var castData = new CombatLogCastData
            {
                CasterId     = attacker.Guid,
                TargetId     = victim.Guid,
                SpellId      = spell.Parameters.SpellInfo.Entry.Id, // TODO: This was updated in order to use ISpell, check if correct
                CombatResult = CombatResult.Hit
            };

            bool deflected   = CalculateDeflect(attacker, victim, out float armorPiercePct);
            if (deflected)
            {
                info.DropEffect = true;
                info.AddCombatLog(new CombatLogDeflect
                    {
                        BMultiHit = false,
                        CastData  = castData
                    });

                victim.ProcManager?.EvaluateProc(ProcTriggerType.OnDeflect, attacker);
                return;
            }

            uint damage = CalculateBaseDamage(attacker, victim, info.Entry);
            damageDescription.RawDamage       = damage;
            damageDescription.RawScaledDamage = damage;

            damage = CalculateBaseDamageVariance(damage);

            damage = GetDamageAfterArmorMitigation(victim, info.Entry.DamageType, damage, armorPiercePct);

            if (CalculateCrit(ref damage, attacker, victim))
                damageDescription.CombatResult = CombatResult.Critical;

            uint preGlanceDamage = damage;
            if (CalculateGlance(ref damage, attacker, victim))
            {
                uint glanceDamage = preGlanceDamage - damage;
                info.AddCombatLog(new CombatLogDeflect
                    {
                        BMultiHit = false,
                        CastData  = castData
                    });
            }

            uint shieldedAmount = CalculateShieldAmount(damage, victim);
            damage -= shieldedAmount;
            damageDescription.ShieldAbsorbAmount = shieldedAmount;

            uint absorptionAbsorbed = Math.Min(victim.Absorption, damage);
            damage -= absorptionAbsorbed;
            damageDescription.AbsorbedAmount = absorptionAbsorbed;

            damageDescription.AdjustedDamage = damage;

            info.AddCombatLog(new CombatLogDamage
            {
                MitigatedDamage = damage,
                RawDamage       = damageDescription.RawDamage,
                Shield          = damageDescription.ShieldAbsorbAmount,
                Absorption      = damageDescription.AbsorbedAmount,
                Overkill        = 0u,
                Glance          = 0u,
                BTargetVulnerable = false,
                BKilled         = false,
                BPeriodic       = false,
                DamageType      = info.Entry.DamageType,
                EffectType      = info.Entry.EffectType,
                CastData        = castData
            });

            if (damageDescription.CombatResult == CombatResult.Hit)
                CalculateMultiHit(attacker, victim, spell, info, ref damage, castData);

            info.AddDamage(damageDescription);

            attacker.ProcManager?.EvaluateProc(ProcTriggerType.OnDamageDealt, victim);
            victim.ProcManager?.EvaluateProc(ProcTriggerType.OnDamageTaken, attacker);

            if (damageDescription.CombatResult == CombatResult.Critical)
            {
                attacker.ProcManager?.EvaluateProc(ProcTriggerType.OnCritDealt, victim);
                victim.ProcManager?.EvaluateProc(ProcTriggerType.OnCritReceived, attacker);
            }
        }

        /// <summary>
        /// Get base damage value for the given <see cref="IUnitEntity"/> with the provided parameter data from the <see cref="Spell4EffectsEntry"/>.
        /// </summary>
        private uint CalculateBaseDamage(IUnitEntity caster, IUnitEntity target, Spell4EffectsEntry entry)
        {
            float basePropertyDamage = CalculateBasePropertyDamage(caster, entry);
            float baseEntityDamage   = CalculateBaseEntityDamage(caster, target, entry);

            float typeMultiplier = 1f;
            float typeBaseDamage = 0;
            switch (entry.EffectType)
            {
                case SpellEffectType.Transference:
                {
                    typeMultiplier = BitConverter.UInt32BitsToSingle(entry.DataBits02);
                    typeBaseDamage = entry.DataBits03;
                    break;
                }
                case SpellEffectType.Damage:
                case SpellEffectType.Heal:
                case SpellEffectType.DistanceDependentDamage:
                case SpellEffectType.DistributedDamage:
                case SpellEffectType.HealShields:
                case SpellEffectType.DamageShields:
                {
                    typeMultiplier = BitConverter.UInt32BitsToSingle(entry.DataBits00);
                    typeBaseDamage = entry.DataBits01;
                    break;
                }
            }

            if (caster.Type is not EntityType.Player and not EntityType.Ghost)
            {
                // TODO: some client code specific to non player entities
            }

            float baseDamage = basePropertyDamage + ((typeBaseDamage + baseEntityDamage) * typeMultiplier);

            float propertyMultiplier = caster.GetProperty((Property)(entry.DamageType + 140)).Value;

            return (uint)(propertyMultiplier * baseDamage);
        }

        private float CalculateBasePropertyDamage(IUnitEntity caster, Spell4EffectsEntry entry)
        {
            float GetProperty(Property property)
            {
                return caster.GetProperty(property)?.Value ?? 0f;
            }

            GameFormulaEntry forumulaEntry = gameTableManager.GameFormula.GetEntry(1266);

            float value = 0f;
            for (int i = 0; i < 4; i++)
            {
                float intermediateValue = 0f;
                switch (entry.ParameterType[i])
                {
                    case SpellEffectParameterType.Brutality:
                        intermediateValue = GetProperty(Property.Strength);
                        break;
                    case SpellEffectParameterType.Finesse:
                        intermediateValue = GetProperty(Property.Dexterity);
                        break;
                    case SpellEffectParameterType.Tech:
                        intermediateValue = GetProperty(Property.Technology);
                        break;
                    case SpellEffectParameterType.Moxie:
                        intermediateValue = GetProperty(Property.Magic);
                        break;
                    case SpellEffectParameterType.Insight:
                        intermediateValue = GetProperty(Property.Wisdom);
                        break;
                    case SpellEffectParameterType.Grit:
                        intermediateValue = GetProperty(Property.Stamina);
                        break;
                    // client defaults to a value of 0.25f if the game table entry is missing
                    case SpellEffectParameterType.AssaultPower:
                        intermediateValue = GetProperty(Property.AssaultRating) * forumulaEntry?.Datafloat0 ?? 0.25f;
                        break;
                    case SpellEffectParameterType.SupportPower:
                        intermediateValue = GetProperty(Property.SupportRating) * forumulaEntry?.Datafloat01 ?? 0.25f;
                        break;
                }

                value += intermediateValue * entry.ParameterValue[i];
            }

            if (value >= 0f)
                return MathF.Ceiling(value);
            else
                return MathF.Floor(value);
        }

        private float CalculateBaseEntityDamage(IUnitEntity caster, IUnitEntity target, Spell4EffectsEntry entry)
        {
            float value = 0f;
            for (int i = 0; i < 4; i++)
            {
                float intermediateValue = 0f;
                switch (entry.ParameterType[i])
                {
                    case SpellEffectParameterType.TargetMaxHealth:
                        intermediateValue = target.MaxHealth;
                        break;
                    case SpellEffectParameterType.CasterMaxHealth:
                        intermediateValue = caster.MaxHealth;
                        break;
                    case SpellEffectParameterType.CasterShieldCapacity:
                        intermediateValue = caster.Shield;
                        break;
                    case SpellEffectParameterType.TargetShieldCapacity:
                        intermediateValue = target.Shield;
                        break;
                    case SpellEffectParameterType.CasterMaxShieldCapacity:
                        intermediateValue = caster.MaxShieldCapacity;
                        break;
                    case SpellEffectParameterType.TargetMaxShieldCapacity:
                        intermediateValue = target.MaxShieldCapacity;
                        break;
                    case SpellEffectParameterType.ItemBudget:
                        intermediateValue = entry.ParameterValue[i];
                        break;
                    case SpellEffectParameterType.TargetCurrentHealth:
                        intermediateValue = target.Health;
                        break;
                    case SpellEffectParameterType.TargetMissingHealth:
                        intermediateValue = (target.MaxHealth - target.Health);
                        break;
                    case SpellEffectParameterType.TargetMissingShields:
                        intermediateValue = (target.MaxShieldCapacity - target.Shield);
                        break;
                    case SpellEffectParameterType.CasterCurrentHealth:
                        intermediateValue = caster.Health;
                        break;
                    case SpellEffectParameterType.CasterMissingHealth:
                        intermediateValue = (caster.MaxHealth - caster.Health);
                        break;
                    case SpellEffectParameterType.CasterMissingShields:
                        intermediateValue = (caster.MaxShieldCapacity - caster.Shield);
                        break;
                    case SpellEffectParameterType.PerLevel:
                        intermediateValue = caster.Level;
                        break;
                }

                value += intermediateValue * entry.ParameterValue[i];
            }

            return value;
        }

        private uint CalculateBaseDamageVariance(uint damage)
        {
            return (uint)(damage * (Random.Shared.Next(95, 103) / 100f));
        }

        private uint GetDamageAfterArmorMitigation(IUnitEntity victim, DamageType damageType, uint damage, float armorPiercePct = 0f)
        {
            GameFormulaEntry armorFormulaEntry = gameTableManager.GameFormula.GetEntry(1234);
            float maximumArmorMitigation = (float)(armorFormulaEntry.Dataint01 * 0.01);
            float victimArmor = victim.GetPropertyValue(Property.Armor) * (1f - armorPiercePct);
            float mitigationPct = (armorFormulaEntry.Datafloat0 / victim.Level * armorFormulaEntry.Datafloat01) * victimArmor / 100;

            if (damageType == DamageType.Physical)
                mitigationPct += victim.GetPropertyValue(Property.DamageMitigationPctOffsetPhysical);
            else if (damageType == DamageType.Tech)
                mitigationPct += victim.GetPropertyValue(Property.DamageMitigationPctOffsetTech);
            else if (damageType == DamageType.Magic)
                mitigationPct += victim.GetPropertyValue(Property.DamageMitigationPctOffsetMagic);

            if (mitigationPct > 0f)
                damage = (uint)Math.Round(damage * (1f - Math.Clamp(mitigationPct, 0f, maximumArmorMitigation)));

            return damage;
        }

        private bool IsSuccessfulChance(float percentage)
        {
            return Random.Shared.Next(1, 10000) <= percentage * 10000f;
        }

        /// <summary>
        /// Calculates and returns the shielded amount of damage.
        /// </summary>
        private uint CalculateShieldAmount(uint damage, IUnitEntity victim)
        {
            uint maxShieldAmount = (uint)(damage * victim.GetPropertyValue(Property.ShieldMitigationMax));
            uint shieldedAmount = Math.Min(victim.Shield, maxShieldAmount);

            return shieldedAmount;
        }

        /// <summary>
        /// Returns a flat damage reduction ratio (0-1) from the target's resist rating for the given damage type.
        /// Uses CC Resilience as the resist chance; on success, a flat portion of ResistPhysical / ResistTech /
        /// ResistMagic (depending on damageType) is applied, producing a 0-1 reduction scale.
        /// </summary>
        private float CalculateResist(IUnitEntity victim, DamageType damageType)
        {
            float resistChance = GetRatingPercentMod(Property.RatingCCResilience, victim);
            if (resistChance <= 0f || !IsSuccessfulChance(resistChance))
                return 0f;

            float resistValue = damageType switch
            {
                DamageType.Physical => victim.GetPropertyValue(Property.ResistPhysical),
                DamageType.Tech     => victim.GetPropertyValue(Property.ResistTech),
                DamageType.Magic    => victim.GetPropertyValue(Property.ResistMagic),
                _                   => 0f,
            };

            return Math.Clamp(resistValue / 100f, 0f, 1f);
        }

        /// <summary>
        /// Calculates strikethrough and returns whether the attack was deflected.
        /// </summary>
        /// <remarks>
        /// Strikethrough reduces the target's block chance using the formula
        /// adjustedBlockChance = blockChance * blockConstant / (strikethrough + blockConstant).
        /// Any excess strikethrough above the adjusted block chance is converted to an armor pierce bonus.
        /// </remarks>
        private bool CalculateDeflect(IUnitEntity attacker, IUnitEntity victim, out float armorPiercePct)
        {
            GameFormulaEntry blockFormulaEntry = gameTableManager.GameFormula.GetEntry(1230);
            float blockConstant = blockFormulaEntry == null ? 100f : blockFormulaEntry.Datafloat01 / attacker.Level;

            float strikethroughPct  = attacker.GetPropertyValue(Property.RatingAvoidReduce);
            float blockChance       = GetRatingPercentMod(Property.RatingAvoidIncrease, victim);

            float adjustedBlockChance = blockChance * blockConstant / (strikethroughPct + blockConstant);
            bool deflected = IsSuccessfulChance(adjustedBlockChance);

            float excessDeflect   = Math.Max(0f, adjustedBlockChance - blockChance);
            armorPiercePct = excessDeflect / blockConstant;

            return deflected;
        }

        /// <summary>
        /// Returns whether this attack crit, and if so, modifies the referenced damage value appropriately.
        /// </summary>
        /// <remarks>
        /// Also handles Crit Deflect — if the target successfully crit-deflects, the crit damage is reduced
        /// by the target's critical mitigation percentage while still being treated as a crit for proc purposes.
        /// </remarks>
        private bool CalculateCrit(ref uint damage, IUnitEntity attacker, IUnitEntity victim)
        {
            float critRate = GetRatingPercentMod(Property.RatingCritChanceIncrease, attacker);
            if (critRate <= 0f)
                return false;

            bool crit = IsSuccessfulChance(critRate);
            if (crit)
            {
                float critSeverity = GetRatingPercentMod(Property.RatingCritSeverityIncrease, attacker);
                damage = (uint)Math.Round(damage * critSeverity);

                // Crit Deflect: target can reduce incoming crit damage
                float critDeflectChance = GetRatingPercentMod(Property.RatingCritDeflectIncrease, victim);
                bool critDeflected = IsSuccessfulChance(critDeflectChance);
                if (critDeflected)
                {
                    float critMitigationPct = GetRatingPercentMod(Property.RatingCritMitigationIncrease, victim);
                    damage = (uint)Math.Round(damage * (1f - critMitigationPct));
                }
            }

            return crit;
        }

        /// <summary>
        /// Returns whether this attack was glanced, and if so, modifies the referenced damage value appropriately.
        /// </summary>
        private bool CalculateGlance(ref uint damage, IUnitEntity attacker, IUnitEntity victim)
        {
            float glanceChance = GetRatingPercentMod(Property.RatingGlanceChance, victim);
            if (glanceChance <= 0f)
                return false;

            bool glance = IsSuccessfulChance(glanceChance);
            if (glance)
                damage = (uint)Math.Round((float)(damage * (1 - GetRatingPercentMod(Property.RatingGlanceAmount, victim))));

            return glance;
        }

        /// <summary>
        /// Multi-Hit adds a second hit after the first when the attack result is a regular Hit (not Crit or Glance).
        /// The multi-hit strike does ~33% of the primary damage. Multi-Hit is only rolled against the victim's
        /// multi-hit chance chance.
        /// </summary>
        private void CalculateMultiHit(IUnitEntity attacker, IUnitEntity victim, ISpell spell, ISpellTargetEffectInfo info, ref uint damage, CombatLogCastData castData)
        {
            float multiHitChance = GetRatingPercentMod(Property.RatingMultiHitChance, attacker);
            if (multiHitChance <= 0f || !IsSuccessfulChance(multiHitChance))
                return;

            uint multiHitDamage = (uint)Math.Round(damage * 0.33f);

            info.AddCombatLog(new CombatLogDamage
            {
                MitigatedDamage   = multiHitDamage,
                RawDamage         = damage,
                Shield            = 0u,
                Absorption        = 0u,
                Overkill          = 0u,
                Glance            = 0u,
                BTargetVulnerable = false,
                BKilled           = false,
                BPeriodic         = false,
                DamageType        = info.Entry.DamageType,
                EffectType        = info.Entry.EffectType,
                CastData          = castData,
            });

            info.AddCombatLog(new CombatLogMultiHit
            {
                DamageAmount      = multiHitDamage,
                RawDamage         = damage,
                Shield            = 0u,
                Absorption        = 0u,
                Overkill          = 0u,
                GlanceAmount      = 0u,
                BTargetVulnerable = false,
                BKilled           = false,
                BPeriodic         = false,
                DamageType        = info.Entry.DamageType,
                EffectType        = info.Entry.EffectType,
                CastData          = castData,
            });

            damage += multiHitDamage;
        }

        private float GetRatingPercentMod(Property property, IUnitEntity entity)
        {
            GameFormulaEntry gameFormula = null;

            switch (property)
            {
                case Property.Armor:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1234);
                    break;
                case Property.RatingArmorPierce:
                case Property.IgnoreArmorBase:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1269);
                    break;
                case Property.RatingAvoidReduce: // Strikethrough
                case Property.BaseAvoidReduceChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1230);
                    break;
                case Property.RatingAvoidIncrease: // Deflect
                case Property.BaseAvoidChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1235);
                    break;
                case Property.RatingCriticalMitigation:
                case Property.BaseCriticalMitigation:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1236);
                    break;
                case Property.RatingCritChanceDecrease: // Deflect Crit Chance
                case Property.BaseAvoidCritChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1236);
                    break;
                case Property.RatingCritChanceIncrease:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1231);
                    break;
                case Property.RatingCritSeverityIncrease:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1232);
                    break;
                case Property.RatingCritDeflectIncrease:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1276);
                    break;
                case Property.RatingCritMitigationIncrease:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1277);
                    break;
                case Property.CCDurationModifier:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1274);
                    break;
                case Property.RatingDamageReflectAmount:
                case Property.BaseDamageReflectAmount:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1272);
                    break;
                case Property.RatingDamageReflectChance:
                case Property.BaseDamageReflectChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1241);
                    break;
                case Property.RatingFocusRecovery:
                case Property.BaseFocusRecoveryInCombat:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1237);
                    break;
                case Property.RatingGlanceAmount:
                case Property.BaseGlanceAmount:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1271);
                    break;
                case Property.RatingGlanceChance:
                case Property.BaseGlanceChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1245);
                    break;
                case Property.RatingIntensity:
                case Property.BaseIntensity:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1243);
                    break;
                case Property.RatingLifesteal:
                case Property.BaseLifesteal:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1233);
                    break;
                case Property.RatingMultiHitAmount:
                case Property.BaseMultiHitAmount:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1270);
                    break;
                case Property.RatingMultiHitChance:
                case Property.BaseMultiHitChance:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1240);
                    break;
                case Property.RatingVigor:
                case Property.BaseVigor:
                    gameFormula = gameTableManager.GameFormula.GetEntry(1244);
                    break;
                default:
                    log.LogWarning($"Unhandled Property in calculating Percentage from Rating: {property}");
                    break;
            }

            // Return a decimal representing the % applied by this rating.
            float ratingMod = ((gameFormula.Datafloat0 / entity.Level) * gameFormula.Datafloat01) * entity.GetPropertyValue(property);
            return Math.Min((GetBasePercentMod(property, entity) + ratingMod) * 100f, gameFormula.Dataint01) / 100f;
        }

        private float GetBasePercentMod(Property property, IUnitEntity entity)
        {
            float baseValue = 0f;
            switch (property)
            {
                case Property.RatingArmorPierce:
                case Property.IgnoreArmorBase:
                    baseValue = entity.GetPropertyValue(Property.IgnoreArmorBase);
                    break;
                case Property.RatingAvoidReduce: // Strikethrough
                case Property.BaseAvoidReduceChance:
                    baseValue = entity.GetPropertyValue(Property.BaseAvoidReduceChance);
                    break;
                case Property.RatingAvoidIncrease: // Deflect
                case Property.BaseAvoidChance:
                    baseValue = entity.GetPropertyValue(Property.BaseAvoidChance);
                    break;
                case Property.RatingCriticalMitigation:
                case Property.BaseCriticalMitigation:
                    baseValue = entity.GetPropertyValue(Property.BaseCriticalMitigation);
                    break;
                case Property.RatingCritChanceDecrease: // Deflect Crit Chance
                case Property.BaseAvoidCritChance:
                    baseValue = entity.GetPropertyValue(Property.BaseAvoidCritChance);
                    break;
                case Property.RatingCritChanceIncrease:
                case Property.BaseCritChance:
                    baseValue = entity.GetPropertyValue(Property.BaseCritChance);
                    break;
                case Property.RatingCritSeverityIncrease:
                    baseValue = entity.GetPropertyValue(Property.CriticalHitSeverityMultiplier);
                    break;
                case Property.RatingCritDeflectIncrease:
                    baseValue = 0f;
                    break;
                case Property.RatingCritMitigationIncrease:
                case Property.RatingDamageReflectAmount:
                case Property.BaseDamageReflectAmount:
                    baseValue = entity.GetPropertyValue(Property.BaseDamageReflectAmount);
                    break;
                case Property.RatingDamageReflectChance:
                case Property.BaseDamageReflectChance:
                    baseValue = entity.GetPropertyValue(Property.BaseDamageReflectChance);
                    break;
                case Property.RatingFocusRecovery:
                case Property.BaseFocusRecoveryInCombat:
                    baseValue = entity.GetPropertyValue(Property.BaseFocusRecoveryInCombat);
                    break;
                case Property.RatingGlanceAmount:
                case Property.BaseGlanceAmount:
                    baseValue = entity.GetPropertyValue(Property.BaseGlanceAmount);
                    break;
                case Property.RatingGlanceChance:
                case Property.BaseGlanceChance:
                    baseValue = entity.GetPropertyValue(Property.BaseGlanceChance);
                    break;
                case Property.RatingIntensity:
                case Property.BaseIntensity:
                    baseValue = entity.GetPropertyValue(Property.BaseIntensity);
                    break;
                case Property.RatingLifesteal:
                case Property.BaseLifesteal:
                    baseValue = entity.GetPropertyValue(Property.BaseLifesteal);
                    break;
                case Property.RatingMultiHitAmount:
                case Property.BaseMultiHitAmount:
                    baseValue = entity.GetPropertyValue(Property.BaseMultiHitAmount);
                    break;
                case Property.RatingMultiHitChance:
                case Property.BaseMultiHitChance:
                    baseValue = entity.GetPropertyValue(Property.BaseMultiHitChance);
                    break;
                case Property.RatingVigor:
                case Property.BaseVigor:
                    baseValue = entity.GetPropertyValue(Property.BaseVigor);
                    break;
                default:
                    log.LogWarning($"Unhandled Property in calculating Percentage from Base: {property}");
                    break;
            }

            return baseValue;
        }
    }
}
