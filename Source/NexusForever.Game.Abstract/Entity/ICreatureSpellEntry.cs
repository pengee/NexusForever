namespace NexusForever.Game.Abstract.Entity
{
    public interface ICreatureSpellEntry
    {
        uint Spell4Id { get; }
        uint Spell4BaseId { get; }
        float MinRange { get; }
        float MaxRange { get; }
    }
}
