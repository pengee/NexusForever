using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.Map.Instance;
using NexusForever.Game.Map.Instance;
using NexusForever.Game.Static.Map;
using NexusForever.GameTable.Model;
using NexusForever.Shared;

namespace NexusForever.Game.Map
{
    public class MapFactory : IMapFactory
    {
        #region Dependency Injection

        private readonly IFactoryInterface<IMap> factory;

        public MapFactory(
            IFactoryInterface<IMap> factory)
        {
            this.factory = factory;
        }

        #endregion

        public IMap CreateMap(WorldEntry entry)
        {
            if (entry.Id == 3460)
                return factory.Resolve<TutorialInstancedMap>();

            switch (entry.Type)
            {
                case MapType.MiniDungeon:
                case MapType.Adventure:
                case MapType.Dungeon:
                    return factory.Resolve<ContentInstancedMap<IContentMapInstance>>();
                case MapType.Pvp:
                    return factory.Resolve<ContentInstancedMap<IContentPvpMapInstance>>();
                case MapType.Residence:
                case MapType.Community:
                    return factory.Resolve<ResidenceInstancedMap>();
                default:
                    return factory.Resolve<IBaseMap>();
            }
        }
    }
}
