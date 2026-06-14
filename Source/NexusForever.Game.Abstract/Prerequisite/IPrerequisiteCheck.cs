using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Static.Prerequisite;

namespace NexusForever.Game.Abstract.Prerequisite
{
    /// <summary>
    /// Evaluates a single prerequisite check for any <see cref="IUnitEntity"/>.
    /// Player-only checks should cast to <see cref="IPlayer"/> and return a default
    /// (e.g. false) when given a non-player entity.
    /// </summary>
    public interface IPrerequisiteCheck
    {
        bool Meets(IUnitEntity entity, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters);

        /// <summary>
        /// Backward-compatible overload for player checks.
        /// </summary>
        bool Meets(IPlayer player, PrerequisiteComparison comparison, uint value, uint objectId, IPrerequisiteParameters parameters)
            => Meets(player, comparison, value, objectId, parameters);
    }
}
