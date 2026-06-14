using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Map;
using NexusForever.Game.Static.Entity.Movement.Command.Mode;
using NexusForever.Script.Template;
using NexusForever.Script.Template.Filter;
using NexusForever.Script.Template.Filter.Dynamic;

namespace NexusForever.Script.Main.AI
{
    [ScriptFilterDefault, ScriptFilterDynamic<IScriptFilterDynamicEntitySpline>]
    public class SplineAI : IWorldEntityScript, IOwnedScript<ICreatureEntity>
    {
        private ICreatureEntity owner;

        public void OnLoad(ICreatureEntity entityOwner)
        {
            owner = entityOwner;
        }

        /// <summary>
        /// Invoked when <see cref="IGridEntity"/> is added to <see cref="IBaseMap"/>.
        /// </summary>
        public void OnAddToMap(IBaseMap map)
        {
            if (owner.Spline == null)
                return;

            // TODO: Handle negative spline speed...
            if (owner.Spline.Speed == -1)
                return;

            owner.MovementManager.SetMode(ModeType.Walk);
            owner.MovementManager.LaunchSpline(owner.Spline.SplineId, owner.Spline.Mode, owner.Spline.Speed, false);
        }
    }
}
