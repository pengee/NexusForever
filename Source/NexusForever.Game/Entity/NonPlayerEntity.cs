using NexusForever.Database.World.Model;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Entity.Movement;
using NexusForever.Game.Static.Entity;
using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using NexusForever.Network.World.Entity;
using NexusForever.Network.World.Entity.Model;
using NexusForever.Script.Template;
using NexusForever.Shared.Game;

namespace NexusForever.Game.Entity
{
    public class NonPlayerEntity : CreatureEntity, INonPlayerEntity
    {
        public override EntityType Type => EntityType.NonPlayer;

        public IVendorInfo VendorInfo { get; private set; }

        private readonly UpdateTimer respawnTimer = new(30d, false);

        #region Dependency Injection

        public NonPlayerEntity(IMovementManager movementManager) : base(movementManager)
        {
            LeashRange = 40f;
        }

        #endregion

        public override void Initialise(EntityModel model)
        {
            base.Initialise(model);

            QuestChecklistIdx = model.QuestChecklistIdx;

            if (model.EntityVendor == null) { return; }
            
            CreateFlags |= EntityCreateFlag.HasInteractionPrereq;
            VendorInfo = new VendorInfo(model);
        }

        public override void Update(double lastTick)
        {
            base.Update(lastTick);

            if (IsAlive) { return; }
            respawnTimer.Update(lastTick);
            
            if (!respawnTimer.HasElapsed) { return; }
            Respawn();
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            respawnTimer.Reset();
        }

        private void Respawn()
        {
            Health = MaxHealth;
            DeathState = null;
            MovementManager.SetPosition(LeashPosition, false);
            Relocate(LeashPosition);
            MovementManager.SetStateDefault();
            MovementManager.SetMoveDefaults(false);

            // Notify scripts so they can perform a defensive reset in case OnDeath was
            // bypassed (e.g. AI was disabled at the time of death).
            scriptCollection?.Invoke<IUnitScript>(s => s.OnRespawn());
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new NonPlayerEntityModel
            {
                CreatureId = CreatureId,
                QuestChecklistIdx = QuestChecklistIdx
            };
        }

        /// <summary>
        /// Calculate default property value for supplied <see cref="Property"/>.
        /// </summary>
        /// <remarks>
        /// Default property values are not sent to the client, they are also calculated by the client and are replaced by any property updates.
        /// </remarks>
        protected override float CalculateDefaultProperty(Property property)
        {
            float value = base.CalculateDefaultProperty(property);

            Creature2Entry creatureEntry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);
            if (creatureEntry == null)
                return value;

            Creature2ArcheTypeEntry archeTypeEntry = GameTableManager.Instance.Creature2ArcheType.GetEntry(creatureEntry.Creature2ArcheTypeId);
            if (archeTypeEntry != null)
                value *= archeTypeEntry.UnitPropertyMultiplier[(uint)property];

            Creature2DifficultyEntry difficultyEntry = GameTableManager.Instance.Creature2Difficulty.GetEntry(creatureEntry.Creature2DifficultyId);
            if (difficultyEntry != null)
                value *= difficultyEntry.UnitPropertyMultiplier[(uint)property];

            Creature2TierEntry tierEntry = GameTableManager.Instance.Creature2Tier.GetEntry(creatureEntry.Creature2TierId);
            if (tierEntry != null)
                value *= tierEntry.UnitPropertyMultiplier[(uint)property];

            return value;
        }
    }
}
