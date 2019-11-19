using System.Collections.Generic;

namespace Zw.MqttMadeBetter.ControlPackets
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
    }
    
    public class MqttSubscribeControlPacket : MqttControlPacket
    {
        public MqttSubscribeControlPacket(ushort packetIdentifier, IReadOnlyList<TopicFilter> topicFilters)
        {
            PacketIdentifier = packetIdentifier;
            TopicFilters = topicFilters;
        }

        public ushort PacketIdentifier { get; }
        public IReadOnlyList<TopicFilter> TopicFilters { get; }
        
        internal override MqttControlPacketType Type => MqttControlPacketType.SUBSCRIBE;
        internal override byte TypeFlags => 0x2;
    }
}