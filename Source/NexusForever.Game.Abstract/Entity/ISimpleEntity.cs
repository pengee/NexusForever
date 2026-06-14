namespace NexusForever.Game.Abstract.Entity
{
    public interface ISimpleEntity : IUnitEntity
    {
        new byte QuestChecklistIdx { get; }
    }
}
