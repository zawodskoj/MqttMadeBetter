namespace Zw.MqttMadeBetter.Channel.ControlPackets
{
    public sealed class MqttPingrespControlPacket : MqttControlPacket, IEmptyPacket
    {
        internal override MqttControlPacketType Type => MqttControlPacketType.PINGRESP;
        internal override byte TypeFlags => 0;
    }
}