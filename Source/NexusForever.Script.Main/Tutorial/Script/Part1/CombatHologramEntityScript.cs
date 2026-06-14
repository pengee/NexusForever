using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    /// <summary>
    /// Progress from P1 -> P2
    /// </summary>
    [ScriptFilterCreatureId((uint) ObjectiveEntities.CombatHologramProjector)]
    public class CombatHologramEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        public void OnActivate(IPlayer activator)
        {
            activator.TeleportToLocal(TutorialLocations.P2StartCombat);
            activator.QuestManager.ObjectiveUpdate(QuestObjectiveType.SucceedCSI, (uint) QuestObjectives.CombatTeleportObjective, 1u);
        }
    }
}