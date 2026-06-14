using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Abstract.Combat
{
    public interface IProcManager
    {
        IUnitEntity Owner { get; }
        IProc AddProc(IUnitEntity caster, ISpellInfo spellInfo, Spell4EffectsEntry effectEntry, uint effectId);
        void RemoveProc(uint effectId);
        void RemoveProcsBySpell(uint spell4Id);
        void RemoveAllProcs();
        void EvaluateProc(ProcTriggerType triggerType, IUnitEntity target = null);
        void Update(double lastTick);
    }
}
