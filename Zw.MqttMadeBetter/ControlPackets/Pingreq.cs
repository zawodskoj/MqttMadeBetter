namespace Zw.MqttMadeBetter.ControlPackets
{
    public sealed class MqttPingreqControlPacket : MqttControlPacket, IEmptyPacket
    {
        internal override MqttControlPacketType Type => MqttControlPacketType.PINGREQ;
        internal override byte TypeFlags => 0;
    }
}