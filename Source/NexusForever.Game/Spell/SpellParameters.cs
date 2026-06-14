using NexusForever.Game.Abstract.Spell;
using NexusForever.Network.World.Entity;

namespace NexusForever.Game.Spell
{
    public class SpellParameters : ISpellParameters
    {
        public ICharacterSpell CharacterSpell { get; set; }
        public ISpellInfo SpellInfo { get; set; }
        public ISpellInfo ParentSpellInfo { get; set; }
        public ISpellInfo RootSpellInfo { get; set; }
        public bool UserInitiatedSpellCast { get; set; }
        public byte TargetType { get; set; } // this should be configured by the spell
        public uint? PrimaryTargetId { get; set; }
        public uint? AttachedUnitId { get; set; }
        public Position[] TelegraphPositions { get; set; }
        public float? Yaw { get; set; }
        public ushort TaxiNode { get; set; }
    }
}
