using NexusForever.Network.Message;
using NexusForever.Network.World.Message.Model;

namespace NexusForever.WorldServer.Network.Message.Handler.Misc
{
    public class ClientPongHandler : IMessageHandler<IWorldSession, ClientPong>
    {
        public void HandleMessage(IWorldSession session, ClientPong ping)
        {
            session.Heartbeat.OnHeartbeat();
        }
    }
}
