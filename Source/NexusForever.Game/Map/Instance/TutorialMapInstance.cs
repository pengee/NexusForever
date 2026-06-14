using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Abstract.Map.Instance;
using NexusForever.Game.Abstract.PublicEvent;
using NexusForever.Game.Static.Reputation;
using NexusForever.Script;
using NexusForever.Script.Template;

namespace NexusForever.Game.Map.Instance
{
    public class TutorialMapInstance : MapInstance, ITutorialMapInstance
    {
        public Faction Faction { get; private set; }

        #region Dependency Injection

        protected readonly IScriptManager scriptManager;

        public TutorialMapInstance(
            IEntityFactory entityFactory,
            IPublicEventManager publicEventManager,
            IScriptManager scriptManager)
            : base(entityFactory, publicEventManager)
        {
            this.scriptManager = scriptManager;
        }

        #endregion

        /// <summary>
        /// Initialise <see cref="ITutorialMapInstance"/> for the supplied faction.
        /// </summary>
        public void Initialise(Faction faction)
        {
            Faction = faction;
        }

        protected override void InitialiseScriptCollection()
        {
            scriptCollection = scriptManager.InitialiseOwnedScripts<ITutorialMapInstance>(this, Entry.Id);
        }

        protected override IMapPosition GetPlayerReturnLocation(IPlayer player)
        {
            throw new NotImplementedException();
        }
    }
}
