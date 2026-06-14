using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Spell;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Combat
{
    public class ProcManager : IProcManager
    {
        private readonly IUnitEntity owner;
        private readonly List<IProc> procs = new();

        public IUnitEntity Owner => owner;

        public ProcManager(IUnitEntity owner)
        {
            this.owner = owner;
        }

        public IProc AddProc(IUnitEntity caster, ISpellInfo spellInfo, Spell4EffectsEntry effectEntry, uint effectId)
        {
            IProc existingProc = procs.FirstOrDefault(p => p.SourceEffectId == effectId);
            if (existingProc != null)
                return existingProc;

            uint castingId = GlobalSpellManager.Instance.NextCastingId;
            IBuff buff = owner.BuffManager.AddBuff(caster, owner, spellInfo, effectEntry, castingId, effectId);
            if (buff == null)
                return null;

            var proc = new Proc(caster, owner, spellInfo, effectEntry, effectId);
            procs.Add(proc);

            buff.ExpireCallback = b =>
            {
                RemoveProc(effectId);
            };

            return proc;
        }

        public void RemoveProc(uint effectId)
        {
            IProc proc = procs.FirstOrDefault(p => p.SourceEffectId == effectId);
            if (proc == null)
                return;

            procs.Remove(proc);

            IBuff buff = owner.BuffManager.GetBuffs(b => b.EffectId == effectId).FirstOrDefault();
            if (buff != null)
                owner.BuffManager.RemoveBuff(buff);
        }

        public void RemoveProcsBySpell(uint spell4Id)
        {
            List<IProc> toRemove = procs.Where(p => p.SourceSpellInfo.Entry.Id == spell4Id).ToList();
            foreach (IProc proc in toRemove)
                RemoveProc(proc.SourceEffectId);
        }

        public void RemoveAllProcs()
        {
            foreach (IProc proc in procs.ToList())
            {
                IBuff buff = owner.BuffManager.GetBuffs(b => b.EffectId == proc.SourceEffectId).FirstOrDefault();
                if (buff != null)
                    owner.BuffManager.RemoveBuff(buff);
            }

            procs.Clear();
        }

        public void EvaluateProc(ProcTriggerType triggerType, IUnitEntity target = null)
        {
            foreach (IProc proc in procs.Where(p => p.TriggerType == triggerType).ToList())
            {
                if (proc.TryTrigger())
                {
                    ApplyIcdCategory(proc.IcdCategory, proc.Cooldown);

                    owner.CastSpell(proc.ProcSpellId, new SpellParameters
                    {
                        UserInitiatedSpellCast = false,
                        PrimaryTargetId = target?.Guid ?? 0u
                    });
                }
            }
        }

        private void ApplyIcdCategory(uint icdCategory, double cooldown)
        {
            if (icdCategory == 0)
                return;

            foreach (IProc proc in procs.Where(p => p.IcdCategory == icdCategory && p.CooldownRemaining < cooldown))
                proc.SetCooldownRemaining(cooldown);
        }

        public void Update(double lastTick)
        {
            foreach (IProc proc in procs)
                proc.Update(lastTick);
        }
    }
}
