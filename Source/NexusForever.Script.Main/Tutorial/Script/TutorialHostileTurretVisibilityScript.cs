using NexusForever.Game.Static.Reputation;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script
{
    /// <summary>
    /// Sets only hostile entities visible
    /// </summary>
    [ScriptFilterCreatureId((uint) ObjectiveEntities.DominionFactionTurret, (uint) ObjectiveEntities.ExileFactionTurret)]
    public class TutorialHostileTurretVisibilityScript : IOwnedScript<IAiTurretEntity>, ICanSeeMeScript
    {
        private IAiTurretEntity owner;

        public void OnLoad(IAiTurretEntity entityOwner) => owner = entityOwner;
        
        public bool CanSeeMe(IGridEntity entity) => entity is IPlayer player && player.GetDispositionTo(owner.Faction1) == Disposition.Hostile;
    }
}