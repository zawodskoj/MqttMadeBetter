namespace Zw.MqttMadeBetter.ControlPackets
{
    public class MqttPubcompControlPacket : MqttControlPacketWithId, IPacketWithOnlyId
    {
        public override ushort PacketIdentifier { get; }

        public MqttPubcompControlPacket(ushort packetIdentifier)
        {
            PacketIdentifier = packetIdentifier;
        }

        internal override MqttControlPacketType Type => MqttControlPacketType.PUBCOMP;
        internal override byte TypeFlags => 0;
    }

    public struct MqttPubcompControlPacketFactory : IPacketWithOnlyIdFactory
    {
        public MqttControlPacket Create(ushort packetIdentifier) => new MqttPubcompControlPacket(packetIdentifier);
    }
}