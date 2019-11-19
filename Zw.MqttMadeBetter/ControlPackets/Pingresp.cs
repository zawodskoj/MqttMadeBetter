namespace Zw.MqttMadeBetter.ControlPackets
{
    public sealed class MqttPingrespControlPacket : MqttControlPacket
    {
        internal override MqttControlPacketType Type => MqttControlPacketType.PINGRESP;
        internal override byte TypeFlags => 0;
    }
}