using System.Numerics;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.Network.World.Entity;
// ReSharper disable PossibleInvalidOperationException

namespace NexusForever.Game.Abstract.Spell
{
    public interface ISpellParameters
    {
        ICharacterSpell CharacterSpell { get; set; }
        ISpellInfo SpellInfo { get; set; }
        ISpellInfo ParentSpellInfo { get; set; }
        ISpellInfo RootSpellInfo { get; set; }
        bool UserInitiatedSpellCast { get; set; }
        byte TargetType { get; set; }
        // IWorldEntity PrimaryTarget { get; set; }
        // IWorldEntity AttachedUnit { get; set; }
        // IWorldEntity VisualPrimaryTarget { get; set; }
        public uint? PrimaryTargetId { get; set; }
        public uint? AttachedUnitId { get; set; }
        Position[] TelegraphPositions { get; set; } //TODO: need to align w/ spell telegraph count
        float? Yaw { get; set; }
        ushort TaxiNode { get; set; }
        
        public SpellEffectTargetFlags GetSpellEffectTargetFlags() => (SpellEffectTargetFlags) TargetType;

        public bool HasPrimaryTarget() => PrimaryTargetId.HasValue;
        public bool HasAttachedUnit() => AttachedUnitId.HasValue;

        public uint GetPrimaryTargetId() => (uint) PrimaryTargetId;
        public uint GetAttachedUnitId() => (uint) AttachedUnitId;
        
        public bool HasTelegraphPositions() => TelegraphPositions is { Length: > 0 };
        
        public Vector3 GetDefaultTelegraphPosition() => TelegraphPositions[0].Vector;
    }
}