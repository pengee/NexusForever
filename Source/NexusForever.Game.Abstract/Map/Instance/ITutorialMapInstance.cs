using NexusForever.Game.Static.Reputation;

namespace NexusForever.Game.Abstract.Map.Instance
{
    public interface ITutorialMapInstance : IMapInstance
    {
        Faction Faction { get; }

        /// <summary>
        /// Initialise <see cref="ITutorialMapInstance"/> for the supplied faction.
        /// </summary>
        void Initialise(Faction faction);
    }
}
