using NexusForever.Game.Abstract.Entity;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    [ScriptFilterScriptName("DirectionArrowPt2EntityScript")]
    public class DirectionArrowPt2EntityScript : NavigatingNexusSequentialPt2EntityScript, IOwnedScript<IWorldEntity>
    {
    }
}
