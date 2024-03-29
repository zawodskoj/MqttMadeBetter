using System.Collections.Generic;

namespace Zw.MqttMadeBetter.Channel.ControlPackets
{
    public class TopicFilter
    {
        public TopicFilter(string topic, MqttMessageQos qos)
        {
            Topic = topic;
            Qos = qos;
        }

        public string Topic { get; }
        public MqttMessageQos Qos { get; }

        public override string ToString()
        {
            return $"{Qos}#{Topic}";
        }
    }
    
    public class MqttSubscribeControlPacket : MqttControlPacketWithId
    {
        public MqttSubscribeControlPacket(ushort packetIdentifier, IReadOnlyList<TopicFilter> topicFilters)
        {
            PacketIdentifier = packetIdentifier;
            TopicFilters = topicFilters;
        }

        public override ushort PacketIdentifier { get; }
        public IReadOnlyList<TopicFilter> TopicFilters { get; }
        
        internal override MqttControlPacketType Type => MqttControlPacketType.SUBSCRIBE;
        internal override byte TypeFlags => 0x2;

        public override string ToString() => $"SUBSCRIBE({PacketIdentifier}): [{string.Join(", ", TopicFilters)}]";
    }
}