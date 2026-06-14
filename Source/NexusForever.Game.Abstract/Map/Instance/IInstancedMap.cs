using System.Collections.Generic;

namespace NexusForever.Game.Abstract.Map.Instance
{
    public interface IInstancedMap : IMap
    {
        /// <summary>
        /// Return all instances for this map.
        /// </summary>
        IEnumerable<IMapInstance> AllInstances { get; }
    }

    public interface IInstancedMap<T> : IInstancedMap where T : IMapInstance
    {
        /// <summary>
        /// Get an existing instance with supplied id.
        /// </summary>
        T GetInstance(Guid instanceId);

        /// <summary>
        /// Return all instances for this map.
        /// </summary>
        IEnumerable<T> Instances { get; }
    }
}
