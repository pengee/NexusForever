using NexusForever.Game.Static.Reputation;

namespace NexusForever.Game.Abstract.Map.Lock
{
    public interface ITutorialMapLock : IMapLock
    {
        Faction? Faction { get; }

        /// <summary>
        /// Initialise <see cref="ITutorialMapLock"/> with the faction for the tutorial lock.
        /// </summary>
        void Initialise(Faction faction);
    }
}
