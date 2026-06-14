using NexusForever.Shared;

namespace NexusForever.Network
{
    public class SocketHeartbeat : IUpdate
    {
        public bool Flatline => timeToFlatline <= 0d;

        private readonly double sessionTimeout;
        private double timeToFlatline;

        public SocketHeartbeat(double sessionTimeout)
        {
            this.sessionTimeout = sessionTimeout;
            OnHeartbeat();
        }

        public void OnHeartbeat()
        {
            timeToFlatline = sessionTimeout;
        }

        public void Update(double lastTick)
        {
            timeToFlatline -= lastTick;
        }
    }
}
