using System.Collections.Generic;
using NexusForever.Game.Abstract.Entity;

namespace NexusForever.Game.Abstract.Prerequisite
{
    public interface IPrerequisiteParameters
    {
        public IUnitEntity Target { get; set; }

        /// <summary>
        /// Set of prerequisite IDs currently being evaluated in this call chain.
        /// Used to detect cycles in prerequisite trees (e.g. A → B → A).
        /// </summary>
        public HashSet<uint> VisitingPrerequisites { get; }
    }
}
