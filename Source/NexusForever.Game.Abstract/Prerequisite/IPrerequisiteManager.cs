using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Abstract.Prerequisite
{
    public interface IPrerequisiteManager
    {
        /// <summary>
        /// Checks if the supplied <see cref="IPlayer"/> meets the given prerequisite.
        /// </summary>
        bool Meets(IPlayer player, uint prerequisiteId);

        /// <summary>
        /// Checks if the supplied <see cref="IPlayer"/> meets the given prerequisite.
        /// </summary>
        bool Meets(IPlayer player, uint prerequisiteId, IPrerequisiteParameters parameters);

        /// <summary>
        /// Checks if the supplied <see cref="IUnitEntity"/> meets the given prerequisite.
        /// </summary>
        bool Meets(IUnitEntity entity, uint prerequisiteId);

        /// <summary>
        /// Checks if the supplied <see cref="IUnitEntity"/> meets the given prerequisite,
        /// using the supplied <see cref="IPrerequisiteParameters"/> (preserves cycle-detection state).
        /// </summary>
        bool Meets(IUnitEntity entity, uint prerequisiteId, IPrerequisiteParameters parameters);
    }
}
