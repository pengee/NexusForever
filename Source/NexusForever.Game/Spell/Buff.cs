using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Spell
{
    public class Buff : IBuff
    {
        public uint CastingId { get; }
        public uint EffectId { get; }
        public IUnitEntity Caster { get; }
        public IUnitEntity Target { get; }
        public ISpellInfo SpellInfo { get; }
        public Spell4EffectsEntry EffectEntry { get; }
        public double Duration { get; }
        public double DurationRemaining { get; private set; }
        public double TickInterval { get; }
        public double TickRemaining { get; private set; }
        public uint StackCount { get; private set; }
        public bool IsExpired { get; private set; }
        public bool IsSuspended { get; set; }
        public bool HasIcon => SpellInfo.BaseInfo.HasIcon;
        public bool IsBuff => SpellInfo.BaseInfo.IsBuff;
        public bool IsDebuff => SpellInfo.BaseInfo.IsDebuff;
        public bool IsDispellable => SpellInfo.BaseInfo.IsDispellable;
        public bool IsChanneled { get; set; }
        public uint AbsorptionRemaining { get; set; }
        public Action<IBuff> ExpireCallback { get; set; }
        public Action<IBuff> TickCallback { get; set; }

        public Buff(IUnitEntity caster, IUnitEntity target, ISpellInfo spellInfo, Spell4EffectsEntry effectEntry, uint castingId, uint effectId)
        {
            Caster      = caster;
            Target      = target;
            SpellInfo   = spellInfo;
            EffectEntry = effectEntry;
            CastingId   = castingId;
            EffectId    = effectId;
            StackCount  = 1u;

            CastMethod castMethod = (CastMethod)spellInfo.BaseInfo.Entry.CastMethod;
            if (castMethod == CastMethod.Channeled && spellInfo.Entry.ChannelMaxTime > 0u)
            {
                IsChanneled  = true;
                Duration     = spellInfo.Entry.ChannelMaxTime / 1000d;
            }
            else
            {
                Duration = effectEntry.DurationTime > 0
                    ? effectEntry.DurationTime / 1000d
                    : spellInfo.Entry.SpellDuration / 1000d;

                TickInterval  = effectEntry.TickTime / 1000d;
                TickRemaining = TickInterval;
            }

            DurationRemaining = Duration;
        }

        public void Update(double lastTick)
        {
            if (IsSuspended)
                return;

            if (Duration > 0d)
            {
                DurationRemaining -= lastTick;
                if (DurationRemaining <= 0d)
                {
                    DurationRemaining = 0d;
                    IsExpired = true;
                    return;
                }
            }
            else if (DurationRemaining == -1d)
            {
                //
            }

            if (TickInterval > 0d)
            {
                TickRemaining -= lastTick;
                if (TickRemaining <= 0d)
                {
                    TickCallback?.Invoke(this);
                    TickRemaining = TickInterval;
                }
            }
        }

        public void Expire()
        {
            DurationRemaining = 0d;
            IsExpired = true;
        }

        public void AddStack()
        {
            StackCount++;
            if (!IsChanneled)
                DurationRemaining = Duration;
        }

        public void ApplyDurationModifier(float modifier)
        {
            DurationRemaining *= modifier;
        }

        public void RestoreSavedState(uint stackCount, double durationRemaining, double tickRemaining)
        {
            StackCount       = stackCount;
            DurationRemaining = durationRemaining;
            TickRemaining     = tickRemaining;
        }
    }
}
