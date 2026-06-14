using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Abstract;
using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Spell.Event;
using NexusForever.Game.Combat;
using NexusForever.Game.Entity;
using NexusForever.Game.Map;
using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Reputation;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Combat;
using NexusForever.Network.World.Message.Model;
using NexusForever.Shared;
using NLog;

namespace NexusForever.Game.Spell
{
    public static class SpellHandler
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        [SpellEffectHandler(SpellEffectType.Damage)]
        public static void HandleEffectDamage(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (!target.CanAttack(spell.Caster))
                return;

            // TODO: once spell effect handlers aren't static, this should be injected without the factory
            var factory = LegacyServiceProvider.Provider.GetService<IFactory<IDamageCalculator>>();
            var damageCalculator = factory.Resolve();
            damageCalculator.CalculateDamage(spell.Caster, target, spell, info);

            target.TakeDamage(spell.Caster, info.Damage);
        }

        [SpellEffectHandler(SpellEffectType.Resurrect)]
        public static void HandleEffectResurrect(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            player.ResurrectionManager.ResurrectRequest(spell.Caster.Guid);
        }

        [SpellEffectHandler(SpellEffectType.Proxy)]
        public static void HandleEffectProxy(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            target.CastSpell(info.Entry.DataBits00, new SpellParameters
            {
                ParentSpellInfo        = spell.Parameters.SpellInfo,
                RootSpellInfo          = spell.Parameters.RootSpellInfo,
                UserInitiatedSpellCast = false
            });
        }
        
        [SpellEffectHandler(SpellEffectType.RavelSignal)]
        public static void HandleEffectRavelSignal(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            // Client-side visual only; no server processing needed
        }

        [SpellEffectHandler(SpellEffectType.Disguise)]
        public static void HandleEffectDisguise(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            Creature2Entry creature2 = GameTableManager.Instance.Creature2.GetEntry(info.Entry.DataBits02);
            if (creature2 == null)
                return;

            Creature2DisplayGroupEntryEntry displayGroupEntry = GameTableManager.Instance.Creature2DisplayGroupEntry.Entries.FirstOrDefault(d => d.Creature2DisplayGroupId == creature2.Creature2DisplayGroupId);
            if (displayGroupEntry == null)
                return;

            target.DisplayInfo = displayGroupEntry.Creature2DisplayInfoId;
        }

        [SpellEffectHandler(SpellEffectType.SummonMount)]
        public static void HandleEffectSummonMount(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            // TODO: handle NPC mounting?
            if (target is not IPlayer player)
                return;

            if (!player.CanMount())
                return;

            // TODO: needs to be replaced once spell effect handlers aren't static
            var factory = LegacyServiceProvider.Provider.GetService<IEntityFactory>();

            var mount = factory.CreateEntity<IMountEntity>();
            mount.Initialise(player, spell.Parameters.SpellInfo.Entry.Id, info.Entry.DataBits00, info.Entry.DataBits01, info.Entry.DataBits04);
            mount.EnqueuePassengerAdd(player, VehicleSeatType.Pilot, 0);

            // usually for hover boards
            /*if (info.Entry.DataBits04 > 0u)
            {
                mount.SetAppearance(new ItemVisual
                {
                    Slot      = ItemSlot.Mount,
                    DisplayId = (ushort)info.Entry.DataBits04
                });
            }*/

            var position = new MapPosition
            {
                Position = player.Position
            };

            if (player.Map.CanEnter(mount, position))
                player.Map.EnqueueAdd(mount, position);
            
            target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            
            // FIXME: also cast 52539,Riding License - Riding Skill 1 - SWC - Tier 1,34464
            // FIXME: also cast 80530,Mount Sprint  - Tier 2,36122
            player.CastSpell(52539, new SpellParameters());
            player.CastSpell(80530, new SpellParameters());
        }

        [SpellEffectHandler(SpellEffectType.Teleport)]
        public static void HandleEffectTeleport(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            WorldLocation2Entry locationEntry = GameTableManager.Instance.WorldLocation2.GetEntry(info.Entry.DataBits00);
            if (locationEntry == null)
                return;

            if (target is IPlayer player)
                if (player.CanTeleport())
                    player.TeleportTo((ushort)locationEntry.WorldId, locationEntry.Position0, locationEntry.Position1, locationEntry.Position2);
        }

        [SpellEffectHandler(SpellEffectType.FullScreenEffect)]
        public static void HandleFullScreenEffect(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (info.Entry.DurationTime == 0u)
                return;

            // Full Screen Effects need explicit duration tracking so the spell finishes at the correct time.
            // Executing spells fire all effects immediately then transition to Finished with no remaining timer,
            // leaving the client waiting for a SpellFinish packet. We enqueue a timed event to call OnTimedFinish()
            // when the effect's duration elapses, which in turn calls SendSpellFinish if status is still Executing.
            if (spell is not Spell concreteSpell)
                return;

            double duration = info.Entry.DurationTime / 1000d;

            concreteSpell.EnqueueEvent(new SpellEvent(duration, concreteSpell.OnTimedFinish));
        }

        [SpellEffectHandler(SpellEffectType.RapidTransport)]
        public static void HandleEffectRapidTransport(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            TaxiNodeEntry taxiNode = GameTableManager.Instance.TaxiNode.GetEntry(spell.Parameters.TaxiNode);
            if (taxiNode == null)
                return;

            WorldLocation2Entry worldLocation = GameTableManager.Instance.WorldLocation2.GetEntry(taxiNode.WorldLocation2Id);
            if (worldLocation == null)
                return;

            if (target is not IPlayer player)
                return;

            if (!player.CanTeleport())
                return;

            var rotation = new Quaternion(worldLocation.Facing0, worldLocation.Facing0, worldLocation.Facing2, worldLocation.Facing3);
            player.Rotation = rotation.ToEuler();
            player.TeleportTo((ushort)worldLocation.WorldId, worldLocation.Position0, worldLocation.Position1, worldLocation.Position2);
        }

        [SpellEffectHandler(SpellEffectType.LearnDyeColor)]
        public static void HandleEffectLearnDyeColor(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            player.Account.GenericUnlockManager.Unlock((ushort)info.Entry.DataBits00);
        }

        [SpellEffectHandler(SpellEffectType.UnlockMount)]
        public static void HandleEffectUnlockMount(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(info.Entry.DataBits00);
            player.SpellManager.AddSpell(spell4Entry.Spell4BaseIdBaseSpell);

            player.Session.EnqueueMessageEncrypted(new ServerUnlockMount
            {
                Spell4Id = info.Entry.DataBits00
            });
        }

        [SpellEffectHandler(SpellEffectType.UnlockPetFlair)]
        public static void HandleEffectUnlockPetFlair(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            player.PetCustomisationManager.UnlockFlair((ushort)info.Entry.DataBits00);
        }

        [SpellEffectHandler(SpellEffectType.UnlockVanityPet)]
        public static void HandleEffectUnlockVanityPet(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(info.Entry.DataBits00);
            player.SpellManager.AddSpell(spell4Entry.Spell4BaseIdBaseSpell);

            player.Session.EnqueueMessageEncrypted(new ServerUnlockMount
            {
                Spell4Id = info.Entry.DataBits00
            });
        }

        [SpellEffectHandler(SpellEffectType.SummonVanityPet)]
        public static void HandleEffectSummonVanityPet(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            // enqueue removal of existing vanity pet if summoned
            if (player.VanityPetGuid != null)
            {
                IPetEntity oldVanityPet = player.GetVisible<IPetEntity>(player.VanityPetGuid.Value);
                oldVanityPet?.RemoveFromMap();
                player.VanityPetGuid = 0u;
            }

            // TODO: needs to be replaced once spell effect handlers aren't static
            var factory = LegacyServiceProvider.Provider.GetService<IEntityFactory>();

            var pet = factory.CreateEntity<IPetEntity>();
            pet.Initialise(player, info.Entry.DataBits00);

            var position = new MapPosition
            {
                Position = player.Position
            };

            if (player.Map.CanEnter(pet, position))
                player.Map.EnqueueAdd(pet, position);
        }

        [SpellEffectHandler(SpellEffectType.TitleGrant)]
        public static void HandleEffectTitleGrant(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            player.TitleManager.AddTitle((ushort)info.Entry.DataBits00);
        }

        [SpellEffectHandler(SpellEffectType.Fluff)]
        public static void HandleEffectFluff(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
        }

        [SpellEffectHandler(SpellEffectType.UnitPropertyModifier)]
        public static void HandleEffectPropertyModifier(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            SpellPropertyModifier modifier = 
                new SpellPropertyModifier((Property)info.Entry.DataBits00, 
                    info.Entry.DataBits01, 
                    BitConverter.UInt32BitsToSingle(info.Entry.DataBits02), 
                    BitConverter.UInt32BitsToSingle(info.Entry.DataBits03), 
                    BitConverter.UInt32BitsToSingle(info.Entry.DataBits04));
            target.AddSpellModifierProperty(modifier, spell.Parameters.SpellInfo.Entry.Id);

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null && buff.Duration > 0d)
                buff.TickCallback = b => { };
        }

        [SpellEffectHandler(SpellEffectType.Heal)]
        public static void HandleEffectHeal(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint healAmount = info.Entry.DataBits00;
            if (healAmount == 0u)
                healAmount = (uint)BitConverter.UInt32BitsToSingle(info.Entry.DataBits02);

            if (healAmount == 0u)
                return;

            uint healingAbsorbed = ApplyHealingAbsorption(target, healAmount);
            healAmount -= healingAbsorbed;

            if (healAmount == 0u)
            {
                info.AddCombatLog(new CombatLogHeal
                {
                    HealAmount  = 0u,
                    Overheal    = 0u,
                    Absorption  = healingAbsorbed,
                    EffectType  = SpellEffectType.Heal,
                    CastData    = new CombatLogCastData
                    {
                        CasterId     = spell.Caster.Guid,
                        TargetId     = target.Guid,
                        SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                        CombatResult = CombatResult.Hit
                    }
                });
                return;
            }

            long actualHeal = Math.Min(healAmount, target.MaxHealth - target.Health);
            uint overheal = (uint)Math.Max(0, healAmount - actualHeal);

            target.ModifyHealth(healAmount, DamageType.Heal, spell.Caster);

            info.AddCombatLog(new CombatLogHeal
            {
                HealAmount  = healAmount,
                Overheal    = overheal,
                Absorption  = healingAbsorbed,
                EffectType  = SpellEffectType.Heal,
                CastData    = new CombatLogCastData
                {
                    CasterId     = spell.Caster.Guid,
                    TargetId     = target.Guid,
                    SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });

            if (info.Entry.DurationTime > 0u)
            {
                IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId, b =>
                {
                    if (!target.IsAlive)
                        return;

                    uint tickHeal = healAmount;
                    uint tickHealingAbsorbed = ApplyHealingAbsorption(target, tickHeal);
                    tickHeal -= tickHealingAbsorbed;

                    if (tickHeal == 0u)
                    {
                        target.EnqueueToVisible(new ServerCombatLog
                        {
                            CombatLog = new CombatLogHeal
                            {
                                HealAmount  = 0u,
                                Overheal    = 0u,
                                Absorption  = tickHealingAbsorbed,
                                EffectType  = SpellEffectType.Heal,
                                CastData    = new CombatLogCastData
                                {
                                    CasterId     = spell.Caster.Guid,
                                    TargetId     = target.Guid,
                                    SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                                    CombatResult = CombatResult.Hit
                                }
                            }
                        }, true);
                        return;
                    }

                    long tickActual = Math.Min(tickHeal, target.MaxHealth - target.Health);
                    uint tickOverheal = (uint)Math.Max(0, tickHeal - tickActual);

                    target.ModifyHealth(tickHeal, DamageType.Heal, spell.Caster);

                    target.EnqueueToVisible(new ServerCombatLog
                    {
                        CombatLog = new CombatLogHeal
                        {
                            HealAmount  = tickHeal,
                            Overheal    = tickOverheal,
                            Absorption  = tickHealingAbsorbed,
                            EffectType  = SpellEffectType.Heal,
                            CastData    = new CombatLogCastData
                            {
                                CasterId     = spell.Caster.Guid,
                                TargetId     = target.Guid,
                                SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                                CombatResult = CombatResult.Hit
                            }
                        }
                    }, true);
                });
            }
        }

        [SpellEffectHandler(SpellEffectType.Absorption)]
        public static void HandleEffectAbsorption(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint amount = info.Entry.DataBits00;
            if (amount == 0u)
                amount = (uint)BitConverter.UInt32BitsToSingle(info.Entry.DataBits02);

            if (amount == 0u)
                return;

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff == null)
                return;

            target.ModifyVital(Vital.Absorption, amount);

            info.AddCombatLog(new CombatLogAbsorption
            {
                AbsorptionAmount = amount,
                CastData = new CombatLogCastData
                {
                    CasterId     = spell.Caster.Guid,
                    TargetId     = target.Guid,
                    SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });

            buff.AbsorptionRemaining += amount;
            buff.ExpireCallback = b =>
            {
                if (b.AbsorptionRemaining > 0u)
                    target.ModifyVital(Vital.Absorption, -b.AbsorptionRemaining);
            };
        }

        [SpellEffectHandler(SpellEffectType.HealingAbsorption)]
        public static void HandleEffectHealingAbsorption(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint amount = info.Entry.DataBits00;
            if (amount == 0u)
                amount = (uint)BitConverter.UInt32BitsToSingle(info.Entry.DataBits02);

            if (amount == 0u)
                return;

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff == null)
                return;

            target.ModifyVital(Vital.HealingAbsorption, amount);

            info.AddCombatLog(new CombatLogHealingAbsorption
            {
                Amount = amount
            });

            buff.AbsorptionRemaining += amount;
            buff.ExpireCallback = b =>
            {
                if (b.AbsorptionRemaining > 0u)
                    target.ModifyVital(Vital.HealingAbsorption, -b.AbsorptionRemaining);
            };
        }

        [SpellEffectHandler(SpellEffectType.VitalModifier)]
        public static void HandleEffectVitalModifier(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            Vital vital = (Vital)info.Entry.DataBits00;
            if (vital == Vital.Invalid)
                return;

            float amount = BitConverter.UInt32BitsToSingle(info.Entry.DataBits02);

            target.ModifyVital(vital, amount);

            info.AddCombatLog(new CombatLogVitalModifier
            {
                Amount         = amount,
                VitalModified  = vital,
                BShowCombatLog = true,
                CastData       = new CombatLogCastData
                {
                    CasterId     = spell.Caster.Guid,
                    TargetId     = target.Guid,
                    SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });

            if (info.Entry.DurationTime > 0u && info.Entry.TickTime > 0u)
            {
                target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId, b =>
                {
                    if (!target.IsAlive)
                        return;

                    target.ModifyVital(vital, amount);

                    target.EnqueueToVisible(new ServerCombatLog
                    {
                        CombatLog = new CombatLogVitalModifier
                        {
                            Amount         = amount,
                            VitalModified  = vital,
                            BShowCombatLog = true,
                            CastData       = new CombatLogCastData
                            {
                                CasterId     = spell.Caster.Guid,
                                TargetId     = target.Guid,
                                SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                                CombatResult = CombatResult.Hit
                            }
                        }
                    }, true);
                });
            }
        }

        [SpellEffectHandler(SpellEffectType.CCStateSet)]
        public static void HandleEffectCCStateSet(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            CCState ccState = (CCState)info.Entry.DataBits00;
            if (info.Entry.DataBits00 > (uint)CCState.AbilityRestriction)
                return;

            CCStatesEntry ccStatesEntry = GameTableManager.Instance.CCStates.GetEntry((ulong)ccState);
            ushort drCategoryId = ccStatesEntry != null ? (ushort)ccStatesEntry.CcStateDiminishingReturnsId : (ushort)0;

            if (target.HasCCState(ccState))
            {
                info.AddCombatLog(new CombatLogCCState
                {
                    State                       = ccState,
                    BRemoved                    = false,
                    InterruptArmorTaken          = 0u,
                    Result                      = CCStateApplyRulesResult.Ok,
                    CcStateDiminishingReturnsId = drCategoryId,
                    CastData                    = new CombatLogCastData
                    {
                        CasterId     = spell.Caster.Guid,
                        TargetId     = target.Guid,
                        SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                        CombatResult = CombatResult.Hit
                    }
                });
                return;
            }

            CCStateApplyRulesResult result = CCStateApplyRulesResult.Ok;
            uint interruptArmorConsumed = 0u;

            if (drCategoryId > 0)
            {
                CCStateApplyRulesResult drResult = target.CheckDiminishingReturns(drCategoryId);
                if (drResult == CCStateApplyRulesResult.DiminishingReturnsTriggerCap)
                {
                    info.AddCombatLog(new CombatLogCCState
                    {
                        State                       = ccState,
                        BRemoved                    = false,
                        InterruptArmorTaken          = 0u,
                        Result                      = CCStateApplyRulesResult.DiminishingReturnsTriggerCap,
                        CcStateDiminishingReturnsId = drCategoryId,
                        CastData                    = new CombatLogCastData
                        {
                            CasterId     = spell.Caster.Guid,
                            TargetId     = target.Guid,
                            SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                            CombatResult = CombatResult.Hit
                        }
                    });
                    info.DropEffect = true;
                    return;
                }
            }

            uint effectiveDurationMs = info.Entry.DurationTime > 0u
                ? info.Entry.DurationTime
                : spell.Parameters.SpellInfo.Entry.SpellDuration;
            if (effectiveDurationMs == 0u)
            {
                log.Warn($"CCState {ccState} for spell {spell.Parameters.SpellInfo.Entry.Id} has no duration in Spell4EffectsEntry.DurationTime or Spell4Entry.SpellDuration; falling back to 1 second default");
                effectiveDurationMs = 1000u;
            }
            double effectiveDuration = effectiveDurationMs / 1000d;

            if (target.InterruptArmor > 0u)
            {
                if (target.InterruptArmor == uint.MaxValue)
                {
                    result = CCStateApplyRulesResult.TargetInfiniteInterruptArmor;
                    info.AddCombatLog(new CombatLogCCState
                    {
                        State                       = ccState,
                        BRemoved                    = false,
                        InterruptArmorTaken          = 0u,
                        Result                      = result,
                        CcStateDiminishingReturnsId = drCategoryId,
                        CastData                    = new CombatLogCastData
                        {
                            CasterId     = spell.Caster.Guid,
                            TargetId     = target.Guid,
                            SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                            CombatResult = CombatResult.Hit
                        }
                    });
                    return;
                }

                interruptArmorConsumed = target.ConsumeInterruptArmor();
                result = interruptArmorConsumed > 0u
                    ? CCStateApplyRulesResult.TargetInterruptArmorReduced
                    : CCStateApplyRulesResult.Ok;
            }

            if (result == CCStateApplyRulesResult.Ok)
                target.ApplyCCState(ccState, spell.CastingId, info.EffectId, spell.Caster, effectiveDuration);

            info.AddCombatLog(new CombatLogCCState
            {
                State                       = ccState,
                BRemoved                    = false,
                InterruptArmorTaken          = interruptArmorConsumed,
                Result                      = result,
                CcStateDiminishingReturnsId = drCategoryId,
                CastData                    = new CombatLogCastData
                {
                    CasterId     = spell.Caster.Guid,
                    TargetId     = target.Guid,
                    SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });

            if (result == CCStateApplyRulesResult.Ok && info.Entry.DurationTime > 0u)
            {
                IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId, null);
                if (buff != null && drCategoryId > 0)
                {
                    float modifier = target.GetDRDurationModifier(drCategoryId);
                    if (modifier < 1.0f)
                        buff.ApplyDurationModifier(modifier);
                    target.RecordDRApplication(drCategoryId);
                }
            }
        }

        [SpellEffectHandler(SpellEffectType.SpellDispel)]
        public static void HandleEffectSpellDispel(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            int count = (int)info.Entry.DataBits00;
            bool dispelDebuff = info.Entry.DataBits01 > 0u;

            List<IBuff> removed = target.BuffManager.RemoveDispellableBuffs((uint)count, dispelDebuff).ToList();

            foreach (IBuff buff in removed)
            {
                info.AddCombatLog(new CombatLogDispel
                {
                    BRemovesSingleInstance = buff.StackCount == 1,
                    InstancesRemoved = buff.StackCount,
                    SpellRemovedId = buff.SpellInfo.Entry.Id
                });
            }
        }

        [SpellEffectHandler(SpellEffectType.SpellForceRemove)]
        public static void HandleEffectSpellForceRemove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint spell4Id = info.Entry.DataBits00;
            target.BuffManager.RemoveBuffsBySpell(spell4Id);
        }

        [SpellEffectHandler(SpellEffectType.SpellForceRemoveChanneled)]
        public static void HandleEffectSpellForceRemoveChanneled(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            target.CancelChanneledSpells();
            target.BuffManager.RemoveChanneledBuffs();
        }

        [SpellEffectHandler(SpellEffectType.ModifyInterruptArmor)]
        public static void HandleEffectModifyInterruptArmor(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            int amount = (int)info.Entry.DataBits00;

            target.ModifyVital(Vital.InterruptArmor, amount);

            info.AddCombatLog(new CombatLogModifyInterruptArmor
            {
                Amount = info.Entry.DataBits00,
                CastData = new CombatLogCastData
                {
                    CasterId = spell.Caster.Guid,
                    TargetId = target.Guid,
                    SpellId = spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });

            if (info.Entry.DurationTime > 0u && info.Entry.TickTime > 0u)
            {
                target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId, b =>
                {
                    if (!target.IsAlive)
                        return;

                    target.ModifyVital(Vital.InterruptArmor, amount);

                    target.EnqueueToVisible(new ServerCombatLog
                    {
                        CombatLog = new CombatLogModifyInterruptArmor
                        {
                            Amount = info.Entry.DataBits00,
                            CastData = new CombatLogCastData
                            {
                                CasterId = spell.Caster.Guid,
                                TargetId = target.Guid,
                                SpellId = spell.Parameters.SpellInfo.Entry.Id,
                                CombatResult = CombatResult.Hit
                            }
                        }
                    }, true);
                });
            }
        }

        [SpellEffectHandler(SpellEffectType.CCStateBreak)]
        public static void HandleEffectCCStateBreak(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            CCState ccState = (CCState)info.Entry.DataBits00;
            if (info.Entry.DataBits00 > (uint)CCState.AbilityRestriction)
                return;

            if (!target.HasCCState(ccState))
                return;

            target.RemoveCCState(ccState, spell.CastingId, info.EffectId);

            info.AddCombatLog(new CombatLogCCStateBreak
            {
                CasterId = spell.Caster.Guid,
                State    = ccState
            });

            foreach (IBuff ccBuff in target.BuffManager.GetBuffs(b =>
                         b.SpellInfo.Entry.Id == spell.Parameters.SpellInfo.Entry.Id).ToList())
            {
                target.BuffManager.RemoveBuff(ccBuff);
            }
        }

        [SpellEffectHandler(SpellEffectType.Proc)]
        public static void HandleEffectProc(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            target.ProcManager.AddProc(spell.Caster, spell.Parameters.SpellInfo, info.Entry, info.EffectId);
        }

        [SpellEffectHandler(SpellEffectType.Stealth)]
        public static void HandleEffectStealth(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            float stealthLevel = BitConverter.UInt32BitsToSingle(info.Entry.DataBits00);

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff == null)
                return;

            target.SetStealth(stealthLevel);

            info.AddCombatLog(new CombatLogStealth
            {
                UnitId   = target.Guid,
                BExiting = false
            });

            buff.ExpireCallback = b =>
            {
                if (b.Target.IsStealthed)
                {
                    b.Target.RemoveStealth();
                    b.Target.EnqueueToVisible(new ServerCombatLog
                    {
                        CombatLog = new CombatLogStealth
                        {
                            UnitId   = b.Target.Guid,
                            BExiting = true
                        }
                    }, true);
                }
            };
        }

        [SpellEffectHandler(SpellEffectType.RemoveStealth)]
        public static void HandleEffectRemoveStealth(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (!target.IsStealthed)
                return;

            target.RemoveStealth();

            info.AddCombatLog(new CombatLogStealth
            {
                UnitId   = target.Guid,
                BExiting = true
            });

            foreach (IBuff stealthBuff in target.BuffManager.GetBuffs(b =>
                         b.EffectEntry.EffectType == SpellEffectType.Stealth).ToList())
            {
                target.BuffManager.RemoveBuff(stealthBuff);
            }
        }

        private static uint ApplyHealingAbsorption(IUnitEntity target, uint healAmount)
        {
            if (target.HealingAbsorption <= 0u || healAmount == 0u)
                return 0u;

            uint healingAbsorbed = Math.Min(target.HealingAbsorption, healAmount);
            target.ModifyVital(Vital.HealingAbsorption, -healingAbsorbed);

            uint toDeduct = healingAbsorbed;
            foreach (IBuff buff in target.BuffManager.GetBuffs(b => b.EffectEntry.EffectType == SpellEffectType.HealingAbsorption && b.AbsorptionRemaining > 0u).ToList())
            {
                uint deduct = Math.Min(buff.AbsorptionRemaining, toDeduct);
                buff.AbsorptionRemaining -= deduct;
                toDeduct -= deduct;
                if (buff.AbsorptionRemaining == 0u)
                    target.BuffManager.RemoveBuff(buff);
                if (toDeduct == 0u)
                    break;
            }

            return healingAbsorbed;
        }

        [SpellEffectHandler(SpellEffectType.ModifySpell)]
        public static void HandleEffectModifySpell(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            log.Warn($"ModifySpell: Spell4Id={spell.Parameters.SpellInfo.Entry.Id}, " +
                     $"DataBits00={info.Entry.DataBits00}, DataBits01={info.Entry.DataBits01}, " +
                     $"DataBits02={info.Entry.DataBits02}, DataBits03={info.Entry.DataBits03}, " +
                     $"DataBits04={info.Entry.DataBits04}");

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null && buff.Duration > 0d)
                buff.TickCallback = b => { };
        }

        [SpellEffectHandler(SpellEffectType.ModifySpellEffect)]
        public static void HandleEffectModifySpellEffect(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            log.Warn($"ModifySpellEffect: Spell4Id={spell.Parameters.SpellInfo.Entry.Id}, " +
                     $"DataBits00={info.Entry.DataBits00}, DataBits01={info.Entry.DataBits01}, " +
                     $"DataBits02={info.Entry.DataBits02}, DataBits03={info.Entry.DataBits03}, " +
                     $"DataBits04={info.Entry.DataBits04}");

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null && buff.Duration > 0d)
                buff.TickCallback = b => { };
        }

        [SpellEffectHandler(SpellEffectType.AddSpell)]
        public static void HandleEffectAddSpell(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            uint spell4BaseId = info.Entry.DataBits00;

            bool wasAdded = false;
            if (player.SpellManager.GetSpell(spell4BaseId) == null)
            {
                try
                {
                    player.SpellManager.AddSpell(spell4BaseId);
                    wasAdded = true;
                }
                catch
                {
                    return;
                }
            }

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null)
            {
                bool added = wasAdded;
                buff.ExpireCallback = b =>
                {
                    if (added && b.Target is IPlayer p)
                        p.SpellManager.RemoveSpell(spell4BaseId);
                };
            }
        }

        [SpellEffectHandler(SpellEffectType.AddSpellEffect)]
        public static void HandleEffectAddSpellEffect(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            log.Warn($"AddSpellEffect: Spell4Id={spell.Parameters.SpellInfo.Entry.Id}, " +
                     $"DataBits00={info.Entry.DataBits00}, DataBits01={info.Entry.DataBits01}, " +
                     $"DataBits02={info.Entry.DataBits02}, DataBits03={info.Entry.DataBits03}, " +
                     $"DataBits04={info.Entry.DataBits04}");

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null && buff.Duration > 0d)
                buff.TickCallback = b => { };
        }

        [SpellEffectHandler(SpellEffectType.SuppressSpellEffect)]
        public static void HandleEffectSuppressSpellEffect(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint spell4BaseId = info.Entry.DataBits00;
            uint effectIndex = info.Entry.DataBits01;

            target.SuppressSpellEffect(spell4BaseId, effectIndex);

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null)
            {
                buff.ExpireCallback = b =>
                {
                    b.Target.UnsuppressSpellEffect(spell4BaseId, effectIndex);
                };
            }
        }

        [SpellEffectHandler(SpellEffectType.HealShields)]
        public static void HandleEffectHealShields(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint healAmount = info.Entry.DataBits00;
            if (healAmount == 0u)
                healAmount = (uint)BitConverter.UInt32BitsToSingle(info.Entry.DataBits02);

            if (healAmount == 0u)
                return;

            uint actualHeal = Math.Min(healAmount, target.MaxShieldCapacity - target.Shield);
            uint overheal = healAmount - actualHeal;

            target.Shield = (uint)Math.Clamp(target.Shield + healAmount, 0u, target.MaxShieldCapacity);

            info.AddCombatLog(new CombatLogHeal
            {
                HealAmount  = healAmount,
                Overheal    = overheal,
                Absorption  = 0u,
                EffectType  = SpellEffectType.HealShields,
                CastData    = new CombatLogCastData
                {
                    CasterId     = spell.Caster.Guid,
                    TargetId     = target.Guid,
                    SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });

            if (info.Entry.DurationTime > 0u)
            {
                target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId, b =>
                {
                    if (!target.IsAlive)
                        return;

                    uint tickHeal = healAmount;
                    uint tickActual = Math.Min(tickHeal, target.MaxShieldCapacity - target.Shield);
                    uint tickOverheal = tickHeal - tickActual;

                    target.Shield = (uint)Math.Clamp(target.Shield + tickHeal, 0u, target.MaxShieldCapacity);

                    target.EnqueueToVisible(new ServerCombatLog
                    {
                        CombatLog = new CombatLogHeal
                        {
                            HealAmount  = tickHeal,
                            Overheal    = tickOverheal,
                            Absorption  = 0u,
                            EffectType  = SpellEffectType.HealShields,
                            CastData    = new CombatLogCastData
                            {
                                CasterId     = spell.Caster.Guid,
                                TargetId     = target.Guid,
                                SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                                CombatResult = CombatResult.Hit
                            }
                        }
                    }, true);
                });
            }
        }

        [SpellEffectHandler(SpellEffectType.DamageShields)]
        public static void HandleEffectDamageShields(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (!spell.Caster.CanAttack(target))
                return;

            uint damageAmount = info.Entry.DataBits00;
            if (damageAmount == 0u)
                damageAmount = (uint)BitConverter.UInt32BitsToSingle(info.Entry.DataBits02);

            if (damageAmount == 0u)
                return;

            if (target.Shield == 0u)
                return;

            uint damageToShields = Math.Min(damageAmount, target.Shield);
            target.Shield -= damageToShields;

            uint overkill = damageAmount - damageToShields;

            info.AddCombatLog(new CombatLogDamageShield
            {
                MitigatedDamage = damageToShields,
                RawDamage       = damageAmount,
                Shield          = damageToShields,
                Absorption      = 0u,
                Overkill        = overkill,
                Glance          = 0u,
                BTargetVulnerable = false,
                BKilled         = false,
                BPeriodic       = false,
                DamageType      = info.Entry.DamageType,
                EffectType      = SpellEffectType.DamageShields,
                CastData        = new CombatLogCastData
                {
                    CasterId     = spell.Caster.Guid,
                    TargetId     = target.Guid,
                    SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });

            if (info.Entry.DurationTime > 0u)
            {
                target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId, b =>
                {
                    if (!target.IsAlive)
                        return;

                    if (target.Shield == 0u)
                        return;

                    uint tickDamage = damageAmount;
                    uint tickDamageToShields = Math.Min(tickDamage, target.Shield);
                    target.Shield -= tickDamageToShields;

                    uint tickOverkill = tickDamage - tickDamageToShields;

                    target.EnqueueToVisible(new ServerCombatLog
                    {
                        CombatLog = new CombatLogDamageShield
                        {
                            MitigatedDamage = tickDamageToShields,
                            RawDamage       = tickDamage,
                            Shield          = tickDamageToShields,
                            Absorption      = 0u,
                            Overkill        = tickOverkill,
                            Glance          = 0u,
                            BTargetVulnerable = false,
                            BKilled         = false,
                            BPeriodic       = true,
                            DamageType      = info.Entry.DamageType,
                            EffectType      = SpellEffectType.DamageShields,
                            CastData        = new CombatLogCastData
                            {
                                CasterId     = spell.Caster.Guid,
                                TargetId     = target.Guid,
                                SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                                CombatResult = CombatResult.Hit
                            }
                        }
                    }, true);
                });
            }
        }

        [SpellEffectHandler(SpellEffectType.ShieldOverload)]
        public static void HandleEffectShieldOverload(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (!spell.Caster.CanAttack(target))
                return;

            uint damageAmount = info.Entry.DataBits00;
            if (damageAmount == 0u)
                damageAmount = (uint)BitConverter.UInt32BitsToSingle(info.Entry.DataBits02);

            if (damageAmount == 0u)
                return;

            if (target.Shield == 0u)
                return;

            uint damageToShields = Math.Min(damageAmount, target.Shield);
            target.Shield -= damageToShields;

            info.AddCombatLog(new CombatLogDamage
            {
                MitigatedDamage = damageToShields,
                RawDamage       = damageAmount,
                Shield          = damageToShields,
                Absorption      = 0u,
                Overkill        = damageAmount - damageToShields,
                Glance          = 0u,
                BTargetVulnerable = false,
                BKilled         = false,
                BPeriodic       = false,
                DamageType      = info.Entry.DamageType,
                EffectType      = SpellEffectType.ShieldOverload,
                CastData        = new CombatLogCastData
                {
                    CasterId     = spell.Caster.Guid,
                    TargetId     = target.Guid,
                    SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });
        }

        [SpellEffectHandler(SpellEffectType.Transference)]
        public static void HandleEffectTransference(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (!spell.Caster.CanAttack(target))
                return;

            var factory = LegacyServiceProvider.Provider.GetService<IFactory<IDamageCalculator>>();
            var damageCalculator = factory.Resolve();
            damageCalculator.CalculateDamage(spell.Caster, target, spell, info);

            uint damageDealt = info.Damage?.AdjustedDamage ?? 0u;
            uint shieldAbsorbed = info.Damage?.ShieldAbsorbAmount ?? 0u;
            uint absorptionAbsorbed = info.Damage?.AbsorbedAmount ?? 0u;

            target.TakeDamage(spell.Caster, info.Damage);

            float transferPct = info.Entry.DataBits01 > 0u
                ? BitConverter.UInt32BitsToSingle(info.Entry.DataBits01)
                : 1f;

            uint healAmount = (uint)(damageDealt * transferPct);

            List<CombatLogTransference.CombatHealData> healedUnits = new();

            if (healAmount > 0u && spell.Caster.IsAlive)
            {
                long actualHeal = Math.Min(healAmount, spell.Caster.MaxHealth - spell.Caster.Health);
                uint casterOverheal = (uint)Math.Max(0, healAmount - actualHeal);

                spell.Caster.ModifyHealth(healAmount, DamageType.Heal, spell.Caster);

                healedUnits.Add(new CombatLogTransference.CombatHealData
                {
                    HealedUnitId = spell.Caster.Guid,
                    HealAmount   = healAmount,
                    Vital        = Vital.Health,
                    Overheal     = casterOverheal,
                    Absorption   = 0u
                });
            }

            info.AddCombatLog(new CombatLogTransference
            {
                DamageAmount      = damageDealt,
                DamageType        = info.Entry.DamageType,
                Shield            = shieldAbsorbed,
                Absorption        = absorptionAbsorbed,
                Overkill          = 0u,
                GlanceAmount      = 0u,
                BTargetVulnerable = false,
                HealedUnits       = healedUnits
            });

            if (info.Entry.DurationTime > 0u)
            {
                target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId, b =>
                {
                    if (!target.IsAlive || !spell.Caster.IsAlive)
                        return;

                    var tickFactory = LegacyServiceProvider.Provider.GetService<IFactory<IDamageCalculator>>();
                    var tickCalc = tickFactory.Resolve();

                    var tickInfo = new SpellTargetInfo.SpellTargetEffectInfo(info.EffectId, info.Entry);
                    tickCalc.CalculateDamage(spell.Caster, target, spell, tickInfo);

                    uint tickDamage = tickInfo.Damage?.AdjustedDamage ?? 0u;
                    uint tickShield = tickInfo.Damage?.ShieldAbsorbAmount ?? 0u;
                    uint tickAbsorption = tickInfo.Damage?.AbsorbedAmount ?? 0u;

                    target.TakeDamage(spell.Caster, tickInfo.Damage);

                    uint tickHeal = (uint)(tickDamage * transferPct);
                    List<CombatLogTransference.CombatHealData> tickHealedUnits = new();

                    if (tickHeal > 0u)
                    {
                        long tickActualHeal = Math.Min(tickHeal, spell.Caster.MaxHealth - spell.Caster.Health);
                        uint tickOverheal = (uint)Math.Max(0, tickHeal - tickActualHeal);

                        spell.Caster.ModifyHealth(tickHeal, DamageType.Heal, spell.Caster);

                        tickHealedUnits.Add(new CombatLogTransference.CombatHealData
                        {
                            HealedUnitId = spell.Caster.Guid,
                            HealAmount   = tickHeal,
                            Vital        = Vital.Health,
                            Overheal     = tickOverheal,
                            Absorption   = 0u
                        });
                    }

                    target.EnqueueToVisible(new ServerCombatLog
                    {
                        CombatLog = new CombatLogTransference
                        {
                            DamageAmount      = tickDamage,
                            DamageType        = info.Entry.DamageType,
                            Shield            = tickShield,
                            Absorption        = tickAbsorption,
                            Overkill          = 0u,
                            GlanceAmount      = 0u,
                            BTargetVulnerable = false,
                            HealedUnits       = tickHealedUnits
                        }
                    }, true);
                });
            }
        }

        [SpellEffectHandler(SpellEffectType.UnitStateSet)]
        public static void HandleEffectUnitStateSet(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint stateFlag = info.Entry.DataBits00;
            if (stateFlag == 0u)
                return;

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null && buff.Duration > 0d)
            {
                buff.ExpireCallback = b =>
                {
                };
                buff.TickCallback = b => { };
            }
        }

        [SpellEffectHandler(SpellEffectType.SummonCreature)]
        public static void HandleEffectSummonCreature(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            Creature2Entry creatureEntry = GameTableManager.Instance.Creature2.GetEntry(info.Entry.DataBits00);
            if (creatureEntry == null)
                return;

            var factory = LegacyServiceProvider.Provider.GetService<IEntityFactory>();
            var creature = factory.CreateEntity<INonPlayerEntity>();
            creature.Initialise(creatureEntry.Id);

            var position = new MapPosition
            {
                Position = spell.Caster.Position
            };

            if (spell.Caster.Map.CanEnter(creature, position))
                spell.Caster.Map.EnqueueAdd(creature, position);

            if (info.Entry.DurationTime > 0u)
            {
                INonPlayerEntity capturedCreature = creature;
                IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
                if (buff != null)
                {
                    buff.ExpireCallback = b =>
                    {
                        if (capturedCreature.InWorld)
                            capturedCreature.RemoveFromMap();
                    };
                    buff.TickCallback = b => { };
                }
            }
        }

        [SpellEffectHandler(SpellEffectType.SummonPet)]
        public static void HandleEffectSummonPet(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            var factory = LegacyServiceProvider.Provider.GetService<IEntityFactory>();
            var pet = factory.CreateEntity<IPetEntity>();
            pet.Initialise(player, info.Entry.DataBits00);

            var position = new MapPosition
            {
                Position = player.Position
            };

            if (player.Map.CanEnter(pet, position))
                player.Map.EnqueueAdd(pet, position);

            if (info.Entry.DurationTime > 0u)
            {
                IPetEntity capturedPet = pet;
                IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
                if (buff != null)
                {
                    buff.ExpireCallback = b =>
                    {
                        if (capturedPet.InWorld)
                            capturedPet.RemoveFromMap();
                    };
                    buff.TickCallback = b => { };
                }
            }
        }

        [SpellEffectHandler(SpellEffectType.ProxyLinearAE)]
        public static void HandleEffectProxyLinearAE(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            target.CastSpell(info.Entry.DataBits00, new SpellParameters
            {
                ParentSpellInfo        = spell.Parameters.SpellInfo,
                RootSpellInfo          = spell.Parameters.RootSpellInfo,
                UserInitiatedSpellCast = false
            });
        }

        [SpellEffectHandler(SpellEffectType.ProxyChannel)]
        public static void HandleEffectProxyChannel(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            target.CastSpell(info.Entry.DataBits00, new SpellParameters
            {
                ParentSpellInfo        = spell.Parameters.SpellInfo,
                RootSpellInfo          = spell.Parameters.RootSpellInfo,
                UserInitiatedSpellCast = false
            });
        }

        [SpellEffectHandler(SpellEffectType.ProxyChannelVariableTime)]
        public static void HandleEffectProxyChannelVariableTime(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            target.CastSpell(info.Entry.DataBits00, new SpellParameters
            {
                ParentSpellInfo        = spell.Parameters.SpellInfo,
                RootSpellInfo          = spell.Parameters.RootSpellInfo,
                UserInitiatedSpellCast = false
            });
        }

        [SpellEffectHandler(SpellEffectType.ProxyRandomExclusive)]
        public static void HandleEffectProxyRandomExclusive(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint[] spellIds = { info.Entry.DataBits00, info.Entry.DataBits01, info.Entry.DataBits02, info.Entry.DataBits03 };
            uint[] validIds = spellIds.Where(id => id != 0u).ToArray();
            if (validIds.Length == 0)
                return;

            uint selectedId = validIds[Random.Shared.Next(validIds.Length)];

            target.CastSpell(selectedId, new SpellParameters
            {
                ParentSpellInfo        = spell.Parameters.SpellInfo,
                RootSpellInfo          = spell.Parameters.RootSpellInfo,
                UserInitiatedSpellCast = false
            });
        }

        [SpellEffectHandler(SpellEffectType.ThreatModification)]
        public static void HandleEffectThreatModification(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            int threatAmount = (int)info.Entry.DataBits00;
            bool isPercentage = info.Entry.DataBits01 != 0u;

            if (isPercentage)
            {
                IHostileEntity hostile = target.ThreatManager.GetHostile(spell.Caster.Guid);
                if (hostile != null)
                    threatAmount = (int)(hostile.Threat * (info.Entry.DataBits00 / 100f));
            }

            target.ThreatManager.UpdateThreat(spell.Caster, threatAmount);
        }

        [SpellEffectHandler(SpellEffectType.ThreatTransfer)]
        public static void HandleEffectThreatTransfer(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            float pct = info.Entry.DataBits00 / 100f;
            target.ThreatManager.TransferThreat(target, spell.Caster, pct);
        }

        [SpellEffectHandler(SpellEffectType.ForcedMove)]
        public static void HandleEffectForcedMove(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint forcedMoveType = info.Entry.DataBits00;
            float speed = BitConverter.UInt32BitsToSingle(info.Entry.DataBits01);

            Vector3 direction;
            if (forcedMoveType == 0u)
            {
                Vector3 diff = target.Position - spell.Caster.Position;
                float length = diff.Length();
                direction = length > 0f ? diff / length : Vector3.UnitX;
            }
            else if (forcedMoveType == 1u)
            {
                Vector3 diff = spell.Caster.Position - target.Position;
                float length = diff.Length();
                direction = length > 0f ? diff / length : Vector3.UnitX;
            }
            else
            {
                direction = new Vector3(
                    BitConverter.UInt32BitsToSingle(info.Entry.DataBits02),
                    BitConverter.UInt32BitsToSingle(info.Entry.DataBits03),
                    BitConverter.UInt32BitsToSingle(info.Entry.DataBits04)
                );
            }

            Vector3 velocity = direction * speed;
            target.MovementManager.SetVelocity(velocity, true);
        }

        [SpellEffectHandler(SpellEffectType.DistanceDependentDamage)]
        public static void HandleEffectDistanceDependentDamage(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (!target.CanAttack(spell.Caster))
                return;

            var factory = LegacyServiceProvider.Provider.GetService<IFactory<IDamageCalculator>>();
            var damageCalculator = factory.Resolve();
            damageCalculator.CalculateDamage(spell.Caster, target, spell, info);

            float maxDistance = BitConverter.UInt32BitsToSingle(info.Entry.DataBits00);
            float minMultiplier = BitConverter.UInt32BitsToSingle(info.Entry.DataBits01);

            float distance = spell.Caster.Position.GetDistance(target.Position);
            float distanceFactor;
            if (maxDistance > 0f)
                distanceFactor = Math.Max(minMultiplier, 1f - (distance / maxDistance) * (1f - minMultiplier));
            else
                distanceFactor = 1f;

            if (info.Damage != null)
            {
                uint scaledDamage = (uint)(info.Damage.AdjustedDamage * distanceFactor);
                info.AddDamage(info.Damage.DamageType, scaledDamage);
            }

            target.TakeDamage(spell.Caster, info.Damage);
        }

        [SpellEffectHandler(SpellEffectType.SpellImmunity)]
        public static void HandleEffectSpellImmunity(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            uint spell4BaseId = info.Entry.DataBits00;
            target.AddSpellImmunity(spell4BaseId);

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null)
            {
                buff.ExpireCallback = b =>
                {
                    b.Target.RemoveSpellImmunity(spell4BaseId);
                };
                buff.TickCallback = b => { };
            }
        }

        [SpellEffectHandler(SpellEffectType.SpellEffectImmunity)]
        public static void HandleEffectSpellEffectImmunity(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            SpellEffectType effectType = (SpellEffectType)info.Entry.DataBits00;
            target.AddSpellEffectImmunity(effectType);

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null)
            {
                buff.ExpireCallback = b =>
                {
                    b.Target.RemoveSpellEffectImmunity(effectType);
                };
                buff.TickCallback = b => { };
            }
        }

        [SpellEffectHandler(SpellEffectType.AggroImmune)]
        public static void HandleEffectAggroImmune(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            target.SetAggroImmune(true);

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null)
            {
                buff.ExpireCallback = b =>
                {
                    b.Target.SetAggroImmune(false);
                };
                buff.TickCallback = b => { };
            }
        }

        [SpellEffectHandler(SpellEffectType.Kill)]
        public static void HandleEffectKill(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            target.ModifyHealth(target.Health, DamageType.Physical, spell.Caster);
        }

        [SpellEffectHandler(SpellEffectType.ModifyAbilityCharges)]
        public static void HandleEffectModifyAbilityCharges(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            uint spell4BaseId = info.Entry.DataBits00;
            int chargeAmount = (int)info.Entry.DataBits01;

            ICharacterSpell characterSpell = player.SpellManager.GetSpell(spell4BaseId);
            if (characterSpell == null)
                return;

            characterSpell.ModifyCharges(chargeAmount);
        }

        [SpellEffectHandler(SpellEffectType.ModifySpellCooldown)]
        public static void HandleEffectModifySpellCooldown(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            uint spell4BaseId = info.Entry.DataBits00;
            double cooldownModMs = info.Entry.DataBits01;

            double currentCooldown = player.SpellManager.GetSpellCooldown(spell4BaseId);
            double newCooldown = Math.Max(0d, currentCooldown + cooldownModMs / 1000d);
            player.SpellManager.SetSpellCooldown(spell4BaseId, newCooldown);
        }

        [SpellEffectHandler(SpellEffectType.ActivateSpellCooldown)]
        public static void HandleEffectActivateSpellCooldown(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            uint spell4BaseId = info.Entry.DataBits00;
            double cooldownSeconds = info.Entry.DataBits01 / 1000d;

            player.SpellManager.SetSpellCooldown(spell4BaseId, cooldownSeconds);
        }

        [SpellEffectHandler(SpellEffectType.CooldownReset)]
        public static void HandleEffectCooldownReset(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            uint spell4BaseId = info.Entry.DataBits00;
            if (spell4BaseId == 0u)
                player.SpellManager.ResetAllSpellCooldowns();
            else
                player.SpellManager.SetSpellCooldown(spell4BaseId, 0d);
        }

        [SpellEffectHandler(SpellEffectType.DistributedDamage)]
        public static void HandleEffectDistributedDamage(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (!target.CanAttack(spell.Caster))
                return;

            var factory = LegacyServiceProvider.Provider.GetService<IFactory<IDamageCalculator>>();
            var damageCalculator = factory.Resolve();
            damageCalculator.CalculateDamage(spell.Caster, target, spell, info);

            target.TakeDamage(spell.Caster, info.Damage);
        }

        [SpellEffectHandler(SpellEffectType.SummonVehicle)]
        public static void HandleEffectSummonVehicle(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is not IPlayer player)
                return;

            if (!player.CanMount())
                return;

            var factory = LegacyServiceProvider.Provider.GetService<IEntityFactory>();

            var vehicle = factory.CreateEntity<IVehicleEntity>();
            vehicle.Initialise(info.Entry.DataBits00, info.Entry.DataBits01, spell.Parameters.SpellInfo.Entry.Id);
            vehicle.EnqueuePassengerAdd(player, VehicleSeatType.Pilot, 0);

            var position = new MapPosition
            {
                Position = player.Position
            };

            if (player.Map.CanEnter(vehicle, position))
                player.Map.EnqueueAdd(vehicle, position);
        }

        [SpellEffectHandler(SpellEffectType.SummonTrap)]
        public static void HandleEffectSummonTrap(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            var factory = LegacyServiceProvider.Provider.GetService<IEntityFactory>();
            var creature = factory.CreateEntity<INonPlayerEntity>();
            creature.Initialise(info.Entry.DataBits00);

            var position = new MapPosition
            {
                Position = target.Position
            };

            if (target.Map.CanEnter(creature, position))
                target.Map.EnqueueAdd(creature, position);

            if (info.Entry.DurationTime > 0u)
            {
                INonPlayerEntity capturedCreature = creature;
                IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
                if (buff != null)
                {
                    buff.ExpireCallback = b =>
                    {
                        if (capturedCreature.InWorld)
                            capturedCreature.RemoveFromMap();
                    };
                    buff.TickCallback = b => { };
                }
            }
        }

        [SpellEffectHandler(SpellEffectType.PetCastSpell)]
        public static void HandleEffectPetCastSpell(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (spell.Caster is not IPlayer player)
                return;

            uint spell4BaseId = info.Entry.DataBits00;

            if (player.VanityPetGuid != null)
            {
                IUnitEntity pet = player.GetVisible<IUnitEntity>(player.VanityPetGuid.Value);
                pet?.CastSpell(spell4BaseId, new SpellParameters
                {
                    ParentSpellInfo        = spell.Parameters.SpellInfo,
                    RootSpellInfo          = spell.Parameters.RootSpellInfo,
                    UserInitiatedSpellCast = false
                });
            }
        }

        [SpellEffectHandler(SpellEffectType.Disembark)]
        public static void HandleEffectDisembark(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (target is IPlayer player)
                player.Dismount();
        }

        [SpellEffectHandler(SpellEffectType.Scale)]
        public static void HandleEffectScale(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            float scale = BitConverter.UInt32BitsToSingle(info.Entry.DataBits00);
            float originalScale = target.MovementManager.GetScale();

            target.MovementManager.SetScale(scale);

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null)
            {
                float capturedScale = originalScale;
                buff.ExpireCallback = b =>
                {
                    b.Target.MovementManager.SetScale(capturedScale);
                };
                buff.TickCallback = b => { };
            }
        }

        [SpellEffectHandler(SpellEffectType.ForceFacing)]
        public static void HandleEffectForceFacing(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (info.Entry.DataBits00 != 0u)
            {
                Vector3 direction = spell.Caster.Position - target.Position;
                float yaw = (float)Math.Atan2(direction.X, direction.Z);
                target.Rotation = new Vector3(0f, yaw, 0f);
            }
            else
            {
                float facing = BitConverter.UInt32BitsToSingle(info.Entry.DataBits01);
                target.Rotation = new Vector3(0f, facing, 0f);
            }
        }

        [SpellEffectHandler(SpellEffectType.FactionSet)]
        public static void HandleEffectFactionSet(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            Faction factionId = (Faction)info.Entry.DataBits00;
            Faction originalFaction = target.Faction1;

            target.SetTemporaryFaction(factionId);

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null)
            {
                buff.ExpireCallback = b =>
                {
                    b.Target.RemoveTemporaryFaction();
                };
                buff.TickCallback = b => { };
            }
        }

        [SpellEffectHandler(SpellEffectType.Activate)]
        public static void HandleEffectActivate(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            if (info.Entry.DataBits00 != 0u)
            {
                target.CastSpell(info.Entry.DataBits00, new SpellParameters
                {
                    ParentSpellInfo        = spell.Parameters.SpellInfo,
                    RootSpellInfo          = spell.Parameters.RootSpellInfo,
                    UserInitiatedSpellCast = false
                });
            }
        }

        [SpellEffectHandler(SpellEffectType.DelayDeath)]
        public static void HandleEffectDelayDeath(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            target.SetDelayDeath(true);

            info.AddCombatLog(new CombatLogDelayDeath
            {
                CastData = new CombatLogCastData
                {
                    CasterId     = spell.Caster.Guid,
                    TargetId     = target.Guid,
                    SpellId      = spell.Parameters.SpellInfo.Entry.Id,
                    CombatResult = CombatResult.Hit
                }
            });

            IBuff buff = target.BuffManager.AddBuff(spell.Caster, target, spell.Parameters.SpellInfo, info.Entry, spell.CastingId, info.EffectId);
            if (buff != null)
            {
                buff.ExpireCallback = b =>
                {
                    b.Target.SetDelayDeath(false);
                };
                buff.TickCallback = b => { };
            }
        }

        [SpellEffectHandler(SpellEffectType.ClampVital)]
        public static void HandleEffectClampVital(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            Vital vital = (Vital)info.Entry.DataBits00;
            if (vital == Vital.Invalid)
                return;

            float min = BitConverter.UInt32BitsToSingle(info.Entry.DataBits01);
            float max = BitConverter.UInt32BitsToSingle(info.Entry.DataBits02);

            float current = target.GetVitalValue(vital);
            float clamped = Math.Clamp(current, min, max);

            float delta = clamped - current;
            if (delta != 0f)
                target.ModifyVital(vital, delta);
        }

        [SpellEffectHandler(SpellEffectType.Script)]
        public static void HandleEffectScript(ISpell spell, IUnitEntity target, ISpellTargetEffectInfo info)
        {
            log.Warn($"Script effect: Spell4Id={spell.Parameters.SpellInfo.Entry.Id}, ScriptId={info.Entry.DataBits00}, " +
                     $"DataBits00={info.Entry.DataBits00}, DataBits01={info.Entry.DataBits01}, " +
                     $"DataBits02={info.Entry.DataBits02}, DataBits03={info.Entry.DataBits03}, " +
                     $"DataBits04={info.Entry.DataBits04}");
        }
    }
}
