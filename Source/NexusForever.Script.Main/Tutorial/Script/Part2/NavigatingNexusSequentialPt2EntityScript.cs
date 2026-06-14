using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    public abstract class NavigatingNexusSequentialPt2EntityScript : ICanSeeMeScript
    {
        private const uint ObjectiveData = 8540;
        
        public bool CanSeeMe(IGridEntity entity)
        {
            if (entity is not IPlayer player)
                return false;

            ushort questId = player.Faction2 == Faction.Exile
                ? (ushort) TutorialReferences.NavigatingNexusExile
                : (ushort) TutorialReferences.NavigatingNexusDominion;

            if (player.QuestManager.GetQuestState (questId) != QuestState.Completed)
                return false;

            ushort questPt2Id = player.Faction2 == Faction.Exile
                ? (ushort) TutorialReferences.NavigatingNexusPt2Exile
                : (ushort) TutorialReferences.NavigatingNexusPt2Dominion;

            IQuest quest = player.QuestManager.GetActiveQuest(questPt2Id);
            if (quest == null)
                return false;

            return !quest.Any(o => o.ObjectiveInfo.Entry.Type == (uint) QuestObjectiveType.EnterArea
                && o.ObjectiveInfo.Entry.Data == ObjectiveData
                && o.IsComplete());
        }
    }
}
