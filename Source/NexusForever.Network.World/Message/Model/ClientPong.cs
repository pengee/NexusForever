using NexusForever.Network.Message;

namespace NexusForever.Network.World.Message.Model
{
    /// <summary>
    /// WildStar build 16042 in-world keep-alive. TODO: guess, real opcode should be confirmed via community
    /// protocol docs or packet analysis.
    /// </summary>
    [Message(GameMessageOpcode.ClientPong)]
    public class ClientPong : IReadable
    {
        public void Read(GamePacketReader reader)
        {
        }
    }
}
