using System;
using Zw.MqttMadeBetter.Channel.ControlPackets;

namespace Zw.MqttMadeBetter.Client
{
    public class MqttMessage
    {
        internal MqttMessage(MqttPublishControlPacket packet)
        {
            IsDuplicate = packet.IsDuplicate;
            Qos = packet.Qos;
            TopicName = packet.TopicName;
            Payload = packet.Payload;
            PacketIdentifier = packet.PacketIdentifier;
        }

        public bool IsDuplicate { get; }
        public MqttMessageQos Qos { get; }
        public string TopicName { get; }
        
        public ReadOnlyMemory<byte> Payload { get; }
        
        internal ushort PacketIdentifier { get; }
    }
}