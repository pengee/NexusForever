using NexusForever.Game.Static.Combat;

namespace NexusForever.Network.World.Combat
{
    public class CombatLogGlance : ICombatLog
    {
        public CombatLogType Type => CombatLogType.Glance;

        public bool BMultiHit { get; set; }
        public CombatLogCastData CastData { get; set; }

        public void Write(GamePacketWriter writer)
        {
            writer.Write(BMultiHit);
            CastData.Write(writer);
        }
    }
}
