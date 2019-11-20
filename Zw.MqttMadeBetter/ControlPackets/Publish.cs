using System;
using System.Text;

namespace Zw.MqttMadeBetter.ControlPackets
{
    public enum MqttMessageQos
    {
        /// <summary>
        /// Quality of Service - level 0 - At most once delivery
        /// </summary>
        QOS_0,
        /// <summary>
        /// Quality of Service - level 1 - At least once delivery
        /// </summary>
        QOS_1,
        /// <summary>
        /// Quality of Service - level 2 - Exactly once delivery
        /// </summary>
        QOS_2
    }
    
    public sealed class MqttPublishControlPacket : MqttControlPacketWithId
    {
        public bool IsDuplicate { get; }
        public MqttMessageQos Qos { get; }
        public bool Retain { get; }
        public string TopicName { get; }
        public override ushort PacketIdentifier { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public MqttPublishControlPacket(bool isDuplicate, MqttMessageQos qos, bool retain, string topicName, ushort packetIdentifier, ReadOnlyMemory<byte> payload)
        {
            IsDuplicate = isDuplicate;
            Qos = qos;
            Retain = retain;
            TopicName = topicName;
            PacketIdentifier = packetIdentifier;
            Payload = payload;
        }

        internal override MqttControlPacketType Type => MqttControlPacketType.PUBLISH;

        internal override byte TypeFlags => (byte) ( 
            (IsDuplicate ? 0x8 : 0) |
            ((byte) Qos << 1) |
            (Retain ? 0x1 : 0));

        public override string ToString()
        {
            var payload = Encoding.UTF8.GetString(Payload.Span);
            return
                $"PUBLISH({PacketIdentifier}): From: {TopicName}/{Qos}, Duplicate: {IsDuplicate}, Payload: {payload};";
        }
    }
}