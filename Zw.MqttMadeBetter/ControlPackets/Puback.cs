namespace Zw.MqttMadeBetter.ControlPackets
{
    public class MqttPubackControlPacket : MqttControlPacket<MqttPubackControlPacketFactory>, IPacketWithOnlyId
    {
        public ushort PacketIdentifier { get; }

        public MqttPubackControlPacket(ushort packetIdentifier)
        {
            PacketIdentifier = packetIdentifier;
        }

        internal override MqttControlPacketType Type => MqttControlPacketType.PUBACK;
        internal override byte TypeFlags => 0;
    }

    public struct MqttPubackControlPacketFactory : IPacketWithOnlyIdFactory
    {
        public MqttControlPacket Create(ushort packetIdentifier) => new MqttPubackControlPacket(packetIdentifier);
    }
}