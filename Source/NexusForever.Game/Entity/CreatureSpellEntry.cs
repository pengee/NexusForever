using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Entity
{
    public class CreatureSpellEntry : ICreatureSpellEntry
    {
        public uint Spell4Id { get; }
        public uint Spell4BaseId { get; }
        public float MinRange { get; }
        public float MaxRange { get; }

        public CreatureSpellEntry(uint spell4Id, uint spell4BaseId, float minRange, float maxRange)
        {
            Spell4Id     = spell4Id;
            Spell4BaseId = spell4BaseId;
            MinRange     = minRange;
            MaxRange     = maxRange;
        }
    }
}
