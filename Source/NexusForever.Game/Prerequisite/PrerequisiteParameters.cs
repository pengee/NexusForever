using System.Collections.Generic;
using NexusForever.Game.Abstract.Entity;
using NexusForever.Game.Abstract.Prerequisite;

namespace NexusForever.Game.Prerequisite
{
    public class PrerequisiteParameters : IPrerequisiteParameters
    {
        public IUnitEntity Target { get; set; }
        public HashSet<uint> VisitingPrerequisites { get; } = new HashSet<uint>();
    }
}
