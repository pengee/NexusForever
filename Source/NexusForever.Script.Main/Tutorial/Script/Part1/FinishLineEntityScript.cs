using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Quest;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    [ScriptFilterCreatureId(73595)]
    public class FinishLineEntityScript : IWorldEntityScript, IOwnedScript<IWorldEntity>
    {
        private const float RangeCheckRadius = 10f;
        private const uint ObjectiveData = 8560;
        
        public void OnLoad(IWorldEntity entityOwner)
        {
            entityOwner.SetInRangeCheck(RangeCheckRadius);
        }

        public void OnEnterRange(IGridEntity entity)
        {
            if (entity is not IPlayer player)
                return;

            player.QuestManager.ObjectiveUpdate(QuestObjectiveType.EnterArea, ObjectiveData, 1);
        }
    }
}
