using NexusForever.Database.World.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Movement;
using NexusForever.Game.Static.Entity;
using NexusForever.Script;
using NexusForever.Network.World.Entity;
using NexusForever.Network.World.Entity.Model;

namespace NexusForever.Game.Entity
{
    public class AiTurretEntity : CreatureEntity, IAiTurretEntity
    {
        public override EntityType Type => EntityType.AiTurret;

        #region Dependency Injection

        public AiTurretEntity(IMovementManager movementManager) : base(movementManager)
        {
        }

        #endregion

        public override void Initialise(EntityModel model)
        {
            base.Initialise(model);
            ScriptManager.Instance.Unload(scriptCollection);
            scriptCollection = ScriptManager.Instance.InitialiseEntityScripts<IAiTurretEntity>(this);
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new AiTurretEntityModel
            {
                CreatureId = CreatureId,
                QuestChecklistIdx = 0
            };
        }
    }
}
