namespace Zw.MqttMadeBetter.ControlPackets
{
    public class MqttUnsubackControlPacket : MqttControlPacket<MqttUnsubackControlPacketFactory>
    {
        public ushort PacketIdentifier { get; }

        public MqttUnsubackControlPacket(ushort packetIdentifier)
        {
            PacketIdentifier = packetIdentifier;
        }

        internal override MqttControlPacketType Type => MqttControlPacketType.UNSUBACK;
        internal override byte TypeFlags => 0;
    }

    public struct MqttUnsubackControlPacketFactory : IPacketWithOnlyIdFactory
    {
        public MqttControlPacket Create(ushort packetIdentifier) => new MqttUnsubackControlPacket(packetIdentifier);
    }
}