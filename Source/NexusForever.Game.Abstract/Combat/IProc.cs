using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Abstract.Combat
{
    public interface IProc
    {
        uint ProcSpellId { get; }
        ProcTriggerType TriggerType { get; }
        float Chance { get; }
        double Cooldown { get; }
        uint IcdCategory { get; }
        IUnitEntity Caster { get; }
        IUnitEntity Target { get; }
        ISpellInfo SourceSpellInfo { get; }
        uint SourceEffectId { get; }
        double CooldownRemaining { get; }
        bool IsOnCooldown { get; }
        void Update(double lastTick);
        bool TryTrigger();
        void ResetCooldown();
        void SetCooldownRemaining(double remaining);
    }
}
