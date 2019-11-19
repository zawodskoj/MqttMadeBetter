namespace Zw.MqttMadeBetter.ControlPackets
{
    public sealed class MqttUnsubackControlPacket : MqttControlPacket
    {
        public ushort PacketIdentifier { get; }

        public MqttUnsubackControlPacket(ushort packetIdentifier)
        {
            PacketIdentifier = packetIdentifier;
        }

        internal override MqttControlPacketType Type => MqttControlPacketType.UNSUBACK;
        internal override byte TypeFlags => 0;
    }
}