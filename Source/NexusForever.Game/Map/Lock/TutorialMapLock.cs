using Microsoft.Extensions.Logging;
using NexusForever.Game.Abstract.Map.Lock;
using NexusForever.Game.Static.Reputation;

namespace NexusForever.Game.Map.Lock
{
    public class TutorialMapLock : MapLock, ITutorialMapLock
    {
        public Faction? Faction { get; private set; }

        #region Dependency Injection

        private readonly ILogger<MapLock> log;

        public TutorialMapLock(
            ILogger<MapLock> log)
            : base(log)
        {
            this.log = log;
        }

        #endregion

        /// <summary>
        /// Initialise <see cref="ITutorialMapLock"/> with the faction for the tutorial lock.
        /// </summary>
        public void Initialise(Faction faction)
        {
            if (Faction.HasValue)
                throw new InvalidOperationException();

            Faction = faction;
            log.LogTrace($"Set faction {faction} for {InstanceId}");
        }
    }
}
