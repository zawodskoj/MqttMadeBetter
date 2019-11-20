using System;

namespace Zw.MqttMadeBetter.Channel.ControlPackets
{
    public class MqttConnectControlPacket : MqttControlPacket
    {
        public MqttConnectControlPacket(string clientId, string username, string password, MqttWillMessage willMessage, bool cleanSession, ushort keepAliveSeconds)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            Username = username;
            Password = password;
            
            if (password != null && username == null)
                throw new ArgumentException("Password must be null if username is null");
            
            WillMessage = willMessage;
            CleanSession = cleanSession;
            KeepAliveSeconds = keepAliveSeconds;
        }

        public string ClientId { get; }
        public string Username { get; }
        public string Password { get; }
        public MqttWillMessage WillMessage { get; }
        public bool CleanSession { get; }
        public ushort KeepAliveSeconds { get; }

        internal override MqttControlPacketType Type => MqttControlPacketType.CONNECT;
        internal override byte TypeFlags => 0;
    }
}