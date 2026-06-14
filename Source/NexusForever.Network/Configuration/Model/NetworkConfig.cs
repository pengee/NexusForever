using NexusForever.Shared.Configuration;

namespace NexusForever.Network.Configuration.Model
{
    [ConfigurationBind]
    public class NetworkConfig
    {
        public string Host { get; set; }
        public ushort Port { get; set; }
        /// <summary>
        /// WildStar build 16042 session timeout in seconds.
        /// </summary>
        public double SessionTimeout { get; set; } = 300d;
    }
}
