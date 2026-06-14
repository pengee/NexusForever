using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Quest;
using NexusForever.Game.Static.Quest;
using NexusForever.Script.Template;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    public abstract class AutoCompleteAndAcceptQuestScript : IQuestScript, IOwnedScript<IQuest>
    {
        public abstract TutorialReferences NextTutorialReferences { get; }

        private IQuest owner;

        public void OnLoad(IQuest entityOwner) => owner = entityOwner;

        public void OnQuestStateChange(QuestState newState, QuestState oldState)
        {
            if (newState != QuestState.Achieved)
                return;

            IPlayer player = owner.GetOwner();
            player.QuestManager.QuestComplete(owner.Id);
            player.QuestManager.QuestAdd((ushort)NextTutorialReferences);
        }
    }
}
