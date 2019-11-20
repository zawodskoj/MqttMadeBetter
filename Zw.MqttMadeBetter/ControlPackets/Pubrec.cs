namespace Zw.MqttMadeBetter.ControlPackets
{
    public class MqttPubrecControlPacket : MqttControlPacketWithId, IPacketWithOnlyId
    {
        public override ushort PacketIdentifier { get; }

        public MqttPubrecControlPacket(ushort packetIdentifier)
        {
            PacketIdentifier = packetIdentifier;
        }

        internal override MqttControlPacketType Type => MqttControlPacketType.PUBREC;
        internal override byte TypeFlags => 0;
    }

    public struct MqttPubrecControlPacketFactory : IPacketWithOnlyIdFactory
    {
        public MqttControlPacket Create(ushort packetIdentifier) => new MqttPubrecControlPacket(packetIdentifier);
    }
}