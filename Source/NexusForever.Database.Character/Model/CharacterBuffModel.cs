namespace NexusForever.Database.Character.Model
{
    public class CharacterBuffModel
    {
        public ulong Id { get; set; }
        public uint Spell4BaseId { get; set; }
        public uint CasterGuid { get; set; }
        public uint StackCount { get; set; }
        public double DurationRemaining { get; set; }
        public double TickRemaining { get; set; }

        public CharacterModel Character { get; set; }
    }
}
