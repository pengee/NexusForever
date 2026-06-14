using NexusForever.Database.Character;
using NexusForever.Database.Character.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Prerequisite;
using NexusForever.Game.Spell;
using NexusForever.Game.Static.Combat.CrowdControl;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Message.Model;
using NexusForever.Network.World.Message.Model.Shared;
using NLog;

namespace NexusForever.Game.Entity
{
    public class BuffManager : IBuffManager
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();
        private readonly IUnitEntity owner;
        private readonly List<IBuff> activeBuffs = new();

        public BuffManager(IUnitEntity owner)
        {
            this.owner = owner;
        }

        public void Save(CharacterContext context)
        {
            IPlayer player = owner as IPlayer;
            if (player == null)
                return;

            context.RemoveRange(context.CharacterBuff.Where(b => b.Id == player.CharacterId));

            foreach (IBuff buff in activeBuffs)
            {
                if (!ShouldPersist(buff, player))
                    continue;

                context.Add(new CharacterBuffModel
                {
                    Id               = player.CharacterId,
                    Spell4BaseId     = buff.SpellInfo.BaseInfo.Entry.Id,
                    CasterGuid       = buff.Caster.Guid,
                    StackCount       = buff.StackCount,
                    DurationRemaining = buff.DurationRemaining,
                    TickRemaining     = buff.TickRemaining
                });
            }
        }

        private bool ShouldPersist(IBuff buff, IPlayer player)
        {
            if (buff.Target != player)
                return false;
            if (!buff.HasIcon)
                return false;
            if (buff.Duration <= 0d || buff.DurationRemaining <= 0d)
                return false;
            if (buff.IsChanneled)
                return false;
            if (buff.IsSuspended)
                return false;
            return true;
        }

        public void Load(IPlayer player, IEnumerable<CharacterBuffModel> models)
        {
            foreach (CharacterBuffModel model in models)
            {
                Spell4BaseEntry spell4BaseEntry = GameTableManager.Instance.Spell4Base.GetEntry(model.Spell4BaseId);
                if (spell4BaseEntry == null)
                    continue;

                ISpellBaseInfo spellBaseInfo = GlobalSpellManager.Instance.GetSpellBaseInfo(model.Spell4BaseId);
                if (spellBaseInfo == null)
                    continue;

                ICharacterSpell characterSpell = player.SpellManager.GetSpell(model.Spell4BaseId);
                if (characterSpell == null)
                    continue;

                byte tier = player.SpellManager.GetSpellTier(model.Spell4BaseId);
                ISpellInfo spellInfo = spellBaseInfo.GetSpellInfo(tier);
                if (spellInfo == null)
                    continue;

                Spell4EffectsEntry effectEntry = spellInfo.Effects.FirstOrDefault();
                if (effectEntry == null)
                    continue;

                uint castingId = GlobalSpellManager.Instance.NextCastingId;
                uint effectId = GlobalSpellManager.Instance.NextEffectId;

                var buff = new Buff(player, player, spellInfo, effectEntry, castingId, effectId);
                buff.RestoreSavedState(model.StackCount, model.DurationRemaining, model.TickRemaining);

                activeBuffs.Add(buff);

                SpellEffectType effectType = (SpellEffectType)effectEntry.EffectType;
                if (effectType == SpellEffectType.UnitPropertyModifier)
                {
                    var modifier = new SpellPropertyModifier(
                        (Property)effectEntry.DataBits00,
                        effectEntry.DataBits01,
                        BitConverter.UInt32BitsToSingle(effectEntry.DataBits02),
                        BitConverter.UInt32BitsToSingle(effectEntry.DataBits03),
                        BitConverter.UInt32BitsToSingle(effectEntry.DataBits04),
                        buff.StackCount);
                    owner.AddSpellModifierProperty(modifier, spellInfo.Entry.Id);
                }
                else if (effectType == SpellEffectType.CCStateSet)
                {
                    CCState ccState = (CCState)effectEntry.DataBits00;
                    if (ccState != 0 && !owner.HasCCState(ccState))
                        owner.ApplyCCState(ccState, castingId, effectId, null, buff.DurationRemaining);
                }
            }
        }

        public void Update(double lastTick)
        {
            //List<IBuff> expiredBuffs = new();

            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                IBuff buff = activeBuffs[i];
                
                UpdateSuspendState(buff);
                CheckPersistencePrerequisites(buff);

                if (!buff.IsExpired)
                {
                    buff.Update(lastTick);
                }
                else
                {
                    RemoveBuff(buff);
                }
            }
            
            // foreach (IBuff buff in activeBuffs)
            // {
            //     UpdateSuspendState(buff);
            //     CheckPersistencePrerequisites(buff);
            //
            //     if (!buff.IsExpired)
            //         buff.Update(lastTick);
            //
            //     if (buff.IsExpired)
            //         expiredBuffs.Add(buff);
            // }
            //
            // foreach (IBuff buff in expiredBuffs)
            //     RemoveBuff(buff);
        }

        private void UpdateSuspendState(IBuff buff)
        {
            if (buff.EffectEntry.PrerequisiteIdTargetSuspend == 0)
                return;

            IPlayer targetPlayer = buff.Target as IPlayer;
            if (targetPlayer == null)
                return;

            try
            {
                bool meets = PrerequisiteManager.Instance.Meets(targetPlayer, buff.EffectEntry.PrerequisiteIdTargetSuspend);
                buff.IsSuspended = !meets;
            }
            catch (Exception e)
            {
                log.Warn(e, $"Exception evaluating PrerequisiteIdTargetSuspend {buff.EffectEntry.PrerequisiteIdTargetSuspend} for buff {buff.SpellInfo.Entry.Id}");
                buff.IsSuspended = false;
            }
        }

        private void CheckPersistencePrerequisites(IBuff buff)
        {
            if (buff.SpellInfo.CasterPersistencePrerequisites != null)
            {
                IPlayer casterPlayer = buff.Caster as IPlayer;
                if (casterPlayer != null)
                {
                    try
                    {
                        if (!PrerequisiteManager.Instance.Meets(casterPlayer, buff.SpellInfo.CasterPersistencePrerequisites.Id))
                        {
                            buff.Expire();
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        log.Warn(e, $"Exception evaluating CasterPersistencePrerequisites {buff.SpellInfo.CasterPersistencePrerequisites.Id} for buff {buff.SpellInfo.Entry.Id}");
                    }
                }
            }

            if (buff.SpellInfo.TargetPersistencePrerequisites != null)
            {
                IPlayer targetPlayer = buff.Target as IPlayer;
                if (targetPlayer != null)
                {
                    try
                    {
                        if (!PrerequisiteManager.Instance.Meets(targetPlayer, buff.SpellInfo.TargetPersistencePrerequisites.Id))
                        {
                            buff.Expire();
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        log.Warn(e, $"Exception evaluating TargetPersistencePrerequisites {buff.SpellInfo.TargetPersistencePrerequisites.Id} for buff {buff.SpellInfo.Entry.Id}");
                    }
                }
            }

            if (buff.EffectEntry.PrerequisiteIdCasterPersistence != 0)
            {
                IPlayer casterPlayer = buff.Caster as IPlayer;
                if (casterPlayer != null)
                {
                    try
                    {
                        if (!PrerequisiteManager.Instance.Meets(casterPlayer, buff.EffectEntry.PrerequisiteIdCasterPersistence))
                        {
                            buff.Expire();
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        log.Warn(e, $"Exception evaluating PrerequisiteIdCasterPersistence {buff.EffectEntry.PrerequisiteIdCasterPersistence} for buff {buff.SpellInfo.Entry.Id}");
                    }
                }
            }

            if (buff.EffectEntry.PrerequisiteIdTargetPersistence != 0)
            {
                IPlayer targetPlayer = buff.Target as IPlayer;
                if (targetPlayer != null)
                {
                    try
                    {
                        if (!PrerequisiteManager.Instance.Meets(targetPlayer, buff.EffectEntry.PrerequisiteIdTargetPersistence))
                        {
                            buff.Expire();
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        log.Warn(e, $"Exception evaluating PrerequisiteIdTargetPersistence {buff.EffectEntry.PrerequisiteIdTargetPersistence} for buff {buff.SpellInfo.Entry.Id}");
                    }
                }
            }
        }

        public IBuff AddBuff(IUnitEntity caster, IUnitEntity target, ISpellInfo spellInfo, Spell4EffectsEntry effectEntry, uint castingId, uint effectId, Action<IBuff> tickCallback = null)
        {
            IBuff existingSameSpell = activeBuffs.FirstOrDefault(b => b.SpellInfo.Entry.Id == spellInfo.Entry.Id && b.Caster.Guid == caster.Guid);
            if (existingSameSpell != null)
            {
                if (spellInfo.StackGroup != null && existingSameSpell.StackCount >= spellInfo.StackGroup.StackCap)
                    return existingSameSpell;

                existingSameSpell.AddStack();
                return existingSameSpell;
            }

            if (spellInfo.StackGroup != null)
            {
                List<IBuff> groupBuffs = activeBuffs
                    .Where(b => b.SpellInfo.StackGroup?.Id == spellInfo.StackGroup.Id)
                    .ToList();

                if (groupBuffs.Count > 0)
                {
                    IBuff highestPriority = groupBuffs.OrderByDescending(b => b.SpellInfo.Entry.StackPriority).First();

                    if (highestPriority.SpellInfo.Entry.StackPriority > spellInfo.Entry.StackPriority)
                        return null;

                    foreach (IBuff groupBuff in groupBuffs.ToList())
                        RemoveBuff(groupBuff);
                }
            }

            var buff = new Buff(caster, target, spellInfo, effectEntry, castingId, effectId)
            {
                TickCallback = tickCallback
            };

            activeBuffs.Add(buff);

            if (caster is IPlayer playerCaster)
                playerCaster.TrackBuffTarget(target.Guid);

            if (buff.HasIcon)
            {
                Server0818 buffMessage = new Server0818
                {
                    CastingId = buff.CastingId,
                    TargetInfo = new TargetInfo
                    {
                        UnitId = buff.Target.Guid,
                        TargetFlags = 1,
                        InstanceCount = 1,
                        CombatResult = CombatResult.Hit,
                        EffectInfoData =
                        [
                            new TargetInfo.EffectInfo
                            {
                                Spell4EffectId = buff.EffectEntry.Id,
                                EffectUniqueId = buff.EffectId,
                                DelayTime = 0,
                                TimeRemaining = buff.DurationRemaining > 0d
                                    ? (int)Math.Ceiling(buff.DurationRemaining)
                                    : -1,
                                InfoType = 0
                            }
                        ]
                    }
                };
                
                owner.EnqueueToVisible(buffMessage, true);
                Console.Error.WriteLine($"[BUFF] {buffMessage.CastingId} Target={buffMessage.TargetInfo.UnitId}, Effect={buffMessage.TargetInfo.EffectInfoData[0].Spell4EffectId}, Id={buffMessage.TargetInfo.EffectInfoData[0].EffectUniqueId}, Time={buffMessage.TargetInfo.EffectInfoData[0].TimeRemaining}");
            }

            return buff;
        }

        public void RemoveBuff(IBuff buff)
        {
            if (!activeBuffs.Remove(buff))
                return;

            buff.ExpireCallback?.Invoke(buff);

            if (buff.EffectEntry.EffectType == SpellEffectType.CCStateSet)
            {
                CCState ccState = (CCState)buff.EffectEntry.DataBits00;
                if (buff.EffectEntry.DataBits00 <= (uint)CCState.AbilityRestriction && owner.HasCCState(ccState))
                    owner.RemoveCCState(ccState, buff.CastingId, buff.EffectId);
            }

            owner.RemoveSpellProperties(buff.SpellInfo.Entry.Id);

            if (buff.HasIcon)
            {
                owner.EnqueueToVisible(new ServerSpellBuffRemove
                {
                    CastingId = buff.CastingId,
                    CasterId  = buff.Caster.Guid
                }, true);
            }
        }

        public void RemoveBuff(uint castingId)
        {
            IBuff buff = GetBuff(castingId);
            if (buff != null)
                RemoveBuff(buff);
        }

        public void RemoveBuffsBySpell(uint spell4Id)
        {
            List<IBuff> buffs = activeBuffs.Where(b => b.SpellInfo.Entry.Id == spell4Id).ToList();
            foreach (IBuff buff in buffs)
                RemoveBuff(buff);
        }

        public void RemoveBuffsByEffectType(SpellEffectType effectType)
        {
            foreach (IBuff buff in activeBuffs.Where(b => b.EffectEntry.EffectType == effectType).ToList())
            {
                RemoveBuff(buff);
            }
        }

        public void RemoveBuffsByCaster(uint casterGuid)
        {
            List<IBuff> buffs = activeBuffs.Where(b => b.Caster.Guid == casterGuid).ToList();
            foreach (IBuff buff in buffs)
                RemoveBuff(buff);
        }

        public void RemoveAllBuffs()
        {
            List<IBuff> buffs = activeBuffs.ToList();
            foreach (IBuff buff in buffs)
                RemoveBuff(buff);
        }

        public IEnumerable<IBuff> GetBuffs()
        {
            return activeBuffs;
        }

        public IEnumerable<IBuff> GetBuffs(Func<IBuff, bool> predicate)
        {
            return activeBuffs.Where(predicate);
        }

        public IBuff GetBuff(uint castingId)
        {
            return activeBuffs.FirstOrDefault(b => b.CastingId == castingId);
        }

        public bool HasBuff(uint castingId)
        {
            return activeBuffs.Any(b => b.CastingId == castingId);
        }

        public IBuff GetBuffBySpell(uint spell4Id, uint casterGuid)
        {
            return activeBuffs.FirstOrDefault(b => b.SpellInfo.Entry.Id == spell4Id && b.Caster.Guid == casterGuid);
        }

        public IBuff GetBuffByEffectId(uint effectId)
        {
            return activeBuffs.FirstOrDefault(b => b.EffectId == effectId);
        }

        public IEnumerable<IBuff> RemoveDispellableBuffs(uint count, bool dispelDebuff)
        {
            var removed = new List<IBuff>();
            List<IBuff> candidates = activeBuffs
                .Where(b => b.IsDispellable && (dispelDebuff ? b.IsDebuff : b.IsBuff))
                .OrderBy(b => b.DurationRemaining)
                .Take((int)count)
                .ToList();

            foreach (IBuff buff in candidates)
            {
                RemoveBuff(buff);
                removed.Add(buff);
            }

            return removed;
        }

        public void RemoveChanneledBuffs()
        {
            List<IBuff> channeledBuffs = activeBuffs.Where(b => b.IsChanneled).ToList();
            foreach (IBuff buff in channeledBuffs)
                RemoveBuff(buff);
        }
    }
}
