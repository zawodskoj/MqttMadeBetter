using System;

namespace Zw.MqttMadeBetter.Channel
{
    public class MqttChannelException : Exception
    {
        public MqttChannelException(string message, Exception innerException) : base(message, innerException) {}
    }
}