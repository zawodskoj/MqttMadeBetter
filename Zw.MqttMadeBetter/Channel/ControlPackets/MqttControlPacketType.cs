namespace Zw.MqttMadeBetter.Channel.ControlPackets
{
    public enum MqttControlPacketType
    {
        /// <summary>
        /// Client request connect to Server
        /// </summary>
        CONNECT = 1,
        /// <summary>
        /// Connect acknowledgement
        /// </summary>
        CONNACK = 2,
        /// <summary>
        /// Publish message
        /// </summary>
        PUBLISH = 3,
        /// <summary>
        /// Publish acknowledgement
        /// </summary>
        PUBACK = 4,
        /// <summary>
        /// Publish received (assured delivery part 1)
        /// </summary>
        PUBREC = 5,
        /// <summary>
        /// Publish release (assured delivery part 2)
        /// </summary>
        PUBREL = 6,
        /// <summary>
        /// Publish complete (assured delivery part 3)
        /// </summary>
        PUBCOMP = 7,
        /// <summary>
        /// Client subscribe request
        /// </summary>
        SUBSCRIBE = 8,
        /// <summary>
        /// Subscribe acknowledgement
        /// </summary>
        SUBACK = 9,
        /// <summary>
        /// Unsubscribe request
        /// </summary>
        UNSUBSCRIBE = 10,
        /// <summary>
        /// Unsubscribe acknowledgement
        /// </summary>
        UNSUBACK = 11,
        /// <summary>
        /// PING request
        /// </summary>
        PINGREQ = 12,
        /// <summary>
        /// PING response
        /// </summary>
        PINGRESP = 13,
        /// <summary>
        /// Client is disconnecting
        /// </summary>
        DISCONNECT = 14
    }
}