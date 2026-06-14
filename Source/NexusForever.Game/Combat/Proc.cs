using NexusForever.Game.Abstract.Combat;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Spell;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;

namespace NexusForever.Game.Combat
{
    public class Proc : IProc
    {
        public uint ProcSpellId { get; }
        public ProcTriggerType TriggerType { get; }
        public float Chance { get; }
        public double Cooldown { get; }
        public uint IcdCategory { get; }
        public IUnitEntity Caster { get; }
        public IUnitEntity Target { get; }
        public ISpellInfo SourceSpellInfo { get; }
        public uint SourceEffectId { get; }

        public double CooldownRemaining { get; private set; }
        public bool IsOnCooldown => CooldownRemaining > 0d;

        public Proc(IUnitEntity caster, IUnitEntity target, ISpellInfo spellInfo, Spell4EffectsEntry effectEntry, uint effectId)
        {
            Caster = caster;
            Target = target;
            SourceSpellInfo = spellInfo;
            SourceEffectId = effectId;

            ProcSpellId = effectEntry.DataBits00;
            TriggerType = (ProcTriggerType)effectEntry.DataBits01;
            Chance = BitConverter.UInt32BitsToSingle(effectEntry.DataBits02);
            Cooldown = effectEntry.DataBits03 / 1000d;
            IcdCategory = effectEntry.DataBits04;
        }

        public void Update(double lastTick)
        {
            if (CooldownRemaining > 0d)
                CooldownRemaining = Math.Max(0d, CooldownRemaining - lastTick);
        }

        public bool TryTrigger()
        {
            if (IsOnCooldown)
                return false;

            if (Random.Shared.NextSingle() > Chance)
                return false;

            CooldownRemaining = Cooldown;
            return true;
        }

        public void ResetCooldown()
        {
            CooldownRemaining = 0d;
        }

        public void SetCooldownRemaining(double remaining)
        {
            CooldownRemaining = remaining;
        }
    }
}
