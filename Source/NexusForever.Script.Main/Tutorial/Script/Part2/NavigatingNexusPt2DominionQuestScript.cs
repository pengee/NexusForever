using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    [ScriptFilterOwnerId(10532)]
    internal class NavigatingNexusPt2DominionQuestScript : AutoCompleteAndAcceptQuestScript
    {
        public override TutorialReferences NextTutorialReferences => TutorialReferences.TheFaceOfTheEnemyDominion;
    }
}
