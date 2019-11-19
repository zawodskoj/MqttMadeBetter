using System.Collections.Generic;

namespace Zw.MqttMadeBetter.ControlPackets
{
    public class MqttUnsubscribeControlPacket : MqttControlPacket<MqttUnsubackControlPacketFactory>, IPacketWithOnlyId
    {
        public MqttUnsubscribeControlPacket(ushort packetIdentifier, IReadOnlyList<string> topics)
        {
            PacketIdentifier = packetIdentifier;
            Topics = topics;
        }

        public ushort PacketIdentifier { get; }
        public IReadOnlyList<string> Topics { get; }
        
        internal override MqttControlPacketType Type => MqttControlPacketType.UNSUBSCRIBE;
        internal override byte TypeFlags => 0x2;
    }
}