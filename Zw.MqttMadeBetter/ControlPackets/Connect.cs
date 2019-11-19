using System;
using System.Diagnostics.CodeAnalysis;

namespace Zw.MqttMadeBetter.ControlPackets
{
    public class MqttWillMessage
    {
        public MqttMessageQos Qos { get; }
        public bool Retain { get; }
        public string Topic { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public MqttWillMessage(MqttMessageQos qos, bool retain, string topic, ReadOnlyMemory<byte> payload)
        {
            Qos = qos;
            Retain = retain;
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
            Payload = payload;
            
            if (payload.Length > 65535)
                throw new ArgumentException("Payload should be smaller than 65536 bytes", nameof(payload));
        }
    }
    
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