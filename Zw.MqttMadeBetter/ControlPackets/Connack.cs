namespace Zw.MqttMadeBetter.ControlPackets
{
    public enum MqttConnackReturnCode
    {
        ACCEPTED = 0,
        UNACCEPTABLE_PROTOCOL = 1,
        IDENTIFIER_REJECTED = 2,
        SERVER_UNAVAILABLE = 3,
        BAD_CREDENTIALS = 4,
        NOT_AUTHORIZED = 5
    }
    
    public sealed class MqttConnackControlPacket : MqttControlPacket
    {
        public MqttConnackControlPacket(bool sessionPresent, MqttConnackReturnCode returnCode)
        {
            SessionPresent = sessionPresent;
            ReturnCode = returnCode;
        }

        internal override MqttControlPacketType Type => MqttControlPacketType.CONNACK;
        internal override byte TypeFlags => 0;

        public bool SessionPresent { get; }
        public MqttConnackReturnCode ReturnCode { get; }

        public override string ToString() => $"CONNACK: SessionPresent: {SessionPresent}, ReturnCode: {ReturnCode}";
    }
}