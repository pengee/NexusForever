using NexusForever.GameTable.Model;

namespace NexusForever.Game.Abstract.Map
{
    public interface IMapFactory
    {
        IMap CreateMap(WorldEntry entry);
    }
}