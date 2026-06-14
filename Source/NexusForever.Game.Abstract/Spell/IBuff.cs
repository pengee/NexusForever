using NexusForever.Game.Abstract.Entity;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Abstract.Spell
{
    public interface IBuff
    {
        uint CastingId { get; }
        uint EffectId { get; }
        IUnitEntity Caster { get; }
        IUnitEntity Target { get; }
        ISpellInfo SpellInfo { get; }
        Spell4EffectsEntry EffectEntry { get; }
        double Duration { get; }
        double DurationRemaining { get; }
        double TickInterval { get; }
        double TickRemaining { get; }
        uint StackCount { get; }
        bool IsExpired { get; }
        bool IsSuspended { get; set; }
        bool HasIcon { get; }
        bool IsBuff { get; }
        bool IsDebuff { get; }
        bool IsDispellable { get; }
        bool IsChanneled { get; set; }
        uint AbsorptionRemaining { get; set; }
        Action<IBuff> ExpireCallback { get; set; }
        Action<IBuff> TickCallback { get; set; }

        void Update(double lastTick);
        void Expire();
        void AddStack();
        void ApplyDurationModifier(float modifier);
        void RestoreSavedState(uint stackCount, double durationRemaining, double tickRemaining);
    }
}
