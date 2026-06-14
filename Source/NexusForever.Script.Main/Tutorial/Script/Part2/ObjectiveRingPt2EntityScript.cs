using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    [ScriptFilterScriptName("ObjectiveRingPt2EntityScript")]
    public class ObjectiveRingPt2EntityScript : NavigatingNexusSequentialPt2EntityScript, IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private const float Distance = 5f;

        public void OnLoad(IWorldEntity entityOwner) => entityOwner.SetInRangeCheck(Distance);

        public void OnEnterRange(IGridEntity entity)
        {
            if (entity is not IPlayer player) { return; }
            
            player.QuestManager.ObjectiveUpdate(QuestObjectiveType.EnterArea, (uint) QuestObjectives.HoverboardObjective, 1);
        }
    }
}
