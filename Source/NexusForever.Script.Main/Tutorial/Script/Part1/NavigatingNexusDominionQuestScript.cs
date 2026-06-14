using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    [ScriptFilterOwnerId(10521)]
    public class NavigatingNexusDominionQuestScript : AutoCompleteAndAcceptQuestScript
    {
        public override TutorialReferences NextTutorialReferences => TutorialReferences.NavigatingNexusPt2Dominion;
    }
}
