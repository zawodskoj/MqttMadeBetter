using System.Collections.Generic;

namespace Zw.MqttMadeBetter.Channel.ControlPackets
{
    public enum SubackResultCode
    {
        QOS_0 = 0,
        QOS_1 = 1,
        QOS_2 = 2,
        FAILURE = 0x80
    }
    
    public sealed class MqttSubackControlPacket : MqttControlPacketWithId
    {
        public override ushort PacketIdentifier { get; }
        public IReadOnlyList<SubackResultCode> Results { get; }

        public MqttSubackControlPacket(ushort packetIdentifier, IReadOnlyList<SubackResultCode> results)
        {
            PacketIdentifier = packetIdentifier;
            Results = results;
        }

        internal override MqttControlPacketType Type => MqttControlPacketType.SUBACK;
        internal override byte TypeFlags => 0;

        public override string ToString() => $"SUBACK({PacketIdentifier}): [{string.Join(", ", Results)}]";
    }
}