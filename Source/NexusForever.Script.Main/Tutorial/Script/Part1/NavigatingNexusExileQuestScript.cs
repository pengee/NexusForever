using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    [ScriptFilterOwnerId(10513)]
    public class NavigatingNexusExileQuestScript : AutoCompleteAndAcceptQuestScript
    {
        public override TutorialReferences NextTutorialReferences => TutorialReferences.NavigatingNexusPt2Exile;
    }
}
