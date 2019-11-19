namespace Zw.MqttMadeBetter.ControlPackets
{
    public class MqttPubrelControlPacket : MqttControlPacket<MqttPubrelControlPacketFactory>, IPacketWithOnlyId
    {
        public ushort PacketIdentifier { get; }

        public MqttPubrelControlPacket(ushort packetIdentifier)
        {
            PacketIdentifier = packetIdentifier;
        }

        internal override MqttControlPacketType Type => MqttControlPacketType.PUBREL;
        internal override byte TypeFlags => 2;
    }

    public struct MqttPubrelControlPacketFactory : IPacketWithOnlyIdFactory
    {
        public MqttControlPacket Create(ushort packetIdentifier) => new MqttPubrelControlPacket(packetIdentifier);
    }
}