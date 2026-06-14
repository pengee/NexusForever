using NexusForever.Script.Template.Filter;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map;
using NexusForever.Script.Template;

namespace NexusForever.Script.Main.Tutorial.Script.Part2
{
    [ScriptFilterCreatureId((uint) ObjectiveEntities.ExileEliteF, (uint) ObjectiveEntities.ExileEliteM)]
    public class TutorialP2EliteExile : IOwnedScript<ICreatureEntity>, IUnitScript
    {
        private ICreatureEntity owner;
        private uint displayInfo;

        public bool IsActive
        {
            get;
            private set
            {
                if(field == value) return;
                field = value;
                owner.DisplayInfo = field ? displayInfo : 0;
                Console.Error.WriteLine($"[TutorialP2EliteExile]: {owner.Guid} Display State changed to {field}!");
            }
        } = true;

        public void OnLoad(ICreatureEntity entityOwner)
        {
            owner = entityOwner;
            displayInfo = owner.DisplayInfo;
            owner.SetDelayDeath(true);
            entityOwner.SetInRangeCheck(30);
        }
        
        public void OnEnterRange(IGridEntity entity)
        {
            if (entity is not IPlayer) { return; }
            IsActive = true;
        }
        
        public void OnExitRange(IGridEntity entity)
        {
            if (entity is not IPlayer) { return; }
            IsActive = false;
        }
        
        public void OnAddToMap(IBaseMap map)
        {
            Console.Error.WriteLine($"[TutorialP2EliteExile]: {owner.Guid} Added to map!");
            
            foreach (string script in owner.ScriptNames)
            {
                Console.Error.WriteLine($"[TutorialP2EliteExile]: {script}");
            }
            
            IsActive = false;
        }
    }
}
