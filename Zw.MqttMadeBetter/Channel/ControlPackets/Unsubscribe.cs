using System.Collections.Generic;

namespace Zw.MqttMadeBetter.Channel.ControlPackets
{
    public class MqttUnsubscribeControlPacket : MqttControlPacketWithId
    {
        public MqttUnsubscribeControlPacket(ushort packetIdentifier, IReadOnlyList<string> topics)
        {
            PacketIdentifier = packetIdentifier;
            Topics = topics;
        }

        public override ushort PacketIdentifier { get; }
        public IReadOnlyList<string> Topics { get; }
        
        internal override MqttControlPacketType Type => MqttControlPacketType.UNSUBSCRIBE;
        internal override byte TypeFlags => 0x2;
    }
}