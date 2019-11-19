namespace Zw.MqttMadeBetter.ControlPackets
{
    public abstract class MqttControlPacket
    {
        internal abstract MqttControlPacketType Type { get; }
        internal abstract byte TypeFlags { get; }

        public override string ToString() => $"{Type}: Flags: {TypeFlags}";
    }
}