using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template.Filter;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Script.Template;

namespace NexusForever.Script.Main.Tutorial.Script
{
    /// <summary>
    /// Sets only hostile creatures visible
    /// </summary>
    [ScriptFilterDefault, ScriptFilterCreatureId((uint) ObjectiveEntities.DominionBattleBeast, (uint) ObjectiveEntities.ExileDefenseDagun, (uint) ObjectiveEntities.DorianWalker, (uint) ObjectiveEntities.ArtemisZin)]
    public class TutorialHostileCreatureVisibilityScript : IOwnedScript<ICreatureEntity>, ICanSeeMeScript
    {
        private ICreatureEntity owner;

        public void OnLoad(ICreatureEntity entityOwner) => owner = entityOwner;
        
        public bool CanSeeMe(IGridEntity entity) => entity is IPlayer player && player.GetDispositionTo(owner.Faction1) == Disposition.Hostile;
    }
}