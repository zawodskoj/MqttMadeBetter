namespace Zw.MqttMadeBetter.Channel.ControlPackets
{
    public class MqttDisconnectControlPacket : MqttControlPacket, IEmptyPacket
    {
        internal override MqttControlPacketType Type => MqttControlPacketType.DISCONNECT;
        internal override byte TypeFlags => 0;
    }
}