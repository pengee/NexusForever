using System.Linq;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    [ScriptFilterCreatureId(73610)]
    public class BarrierEntityScript : ICanSeeMeScript, IOwnedScript<IWorldEntity>
    {
        public const uint ObjectiveData = 8540;

        public bool CanSeeMe(IGridEntity entity)
        {
            if (entity is not IPlayer player)
                return true;

            ushort questId = player.Faction2 == Faction.Exile
                ? (ushort)TutorialReferences.NavigatingNexusPt2Exile
                : (ushort)TutorialReferences.NavigatingNexusPt2Dominion;

            IQuest quest = player.QuestManager.GetActiveQuest(questId);
            if (quest == null)
                return true;

            bool enteredPt2Area = quest.Any(o =>
                o.ObjectiveInfo.Entry.Type == (uint)QuestObjectiveType.EnterArea
                && o.ObjectiveInfo.Entry.Data == ObjectiveData
                && o.IsComplete());

            return !enteredPt2Area;
        }
    }
}
