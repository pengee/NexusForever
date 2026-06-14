using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part1
{
    [ScriptFilterOwnerId(10527)]
    internal class NavigatingNexusPt2ExileQuestScript : AutoCompleteAndAcceptQuestScript
    {
        public override TutorialReferences NextTutorialReferences => TutorialReferences.TheFaceOfTheEnemyExile;
    }
}
