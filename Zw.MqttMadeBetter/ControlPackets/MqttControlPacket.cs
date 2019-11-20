namespace Zw.MqttMadeBetter.ControlPackets
{
    public abstract class MqttControlPacket
    {
        internal abstract MqttControlPacketType Type { get; }
        internal abstract byte TypeFlags { get; }

        public override string ToString() => $"{Type}: Flags: {TypeFlags}";
    }

    public abstract class MqttControlPacketWithId : MqttControlPacket
    {
        public abstract ushort PacketIdentifier { get; }
    }

    internal interface IEmptyPacket 
    {
    
    }

    internal interface IPacketWithOnlyId
    {
        ushort PacketIdentifier { get; }
    }

    internal interface IPacketWithOnlyIdFactory
    {
        MqttControlPacket Create(ushort packetIdentifier);
    }
}