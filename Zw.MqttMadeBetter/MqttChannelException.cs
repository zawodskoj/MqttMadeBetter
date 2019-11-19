using System;

namespace Zw.MqttMadeBetter
{
    public class MqttChannelException : Exception
    {
        public MqttChannelException(string message, Exception innerException) : base(message, innerException) {}
    }
}