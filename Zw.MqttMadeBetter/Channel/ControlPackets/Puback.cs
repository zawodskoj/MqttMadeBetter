namespace Zw.MqttMadeBetter.Channel.ControlPackets
{
    public class MqttPubackControlPacket : MqttControlPacketWithId, IPacketWithOnlyId
    {
        public override ushort PacketIdentifier { get; }

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