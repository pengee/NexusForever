using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;
using NexusForever.Game.Static.Reputation;
using NexusForever.Script.Template;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    public abstract class NavigatingNexusSequentialEntityScript : ICanSeeMeScript, IOwnedScript<IWorldEntity>
    {
        protected IWorldEntity Owner;

        public virtual void OnLoad(IWorldEntity entityOwner) => Owner = entityOwner;

        public bool CanSeeMe(IGridEntity entity)
        {
            if (entity is not IPlayer player) { return false; }

            ushort questId = player.Faction2 == Faction.Exile
                ? (ushort)TutorialReferences.NavigatingNexusExile
                : (ushort)TutorialReferences.NavigatingNexusDominion;

            IQuest quest = player.QuestManager.GetActiveQuest(questId);
            if (quest == null)
                return false;

            return Owner.QuestChecklistIdx switch
            {
                0 => !IsEnterAreaComplete(quest, (uint) QuestObjectives.AreaPressurePlateOne),
                1 => IsEnterAreaComplete(quest, (uint) QuestObjectives.AreaPressurePlateOne) && !IsEnterAreaComplete(quest, (uint) QuestObjectives.AreaPressurePlateTwo),
                2 => IsEnterAreaComplete(quest, (uint) QuestObjectives.AreaPressurePlateTwo) && !IsEnterAreaComplete(quest, (uint) QuestObjectives.AreaPressurePlateThree),
                3 => IsEnterAreaComplete(quest, (uint) QuestObjectives.AreaPressurePlateThree),
                _ => false,
            };
        }

        private static bool IsEnterAreaComplete(IQuest quest, uint data)
        {
            return quest.Any(o => o.ObjectiveInfo.Entry.Type == (uint) QuestObjectiveType.EnterArea
                && o.ObjectiveInfo.Entry.Data == data
                && o.IsComplete());
        }
    }
}
