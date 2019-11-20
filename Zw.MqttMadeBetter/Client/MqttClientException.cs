using System;

namespace Zw.MqttMadeBetter.Client
{
    public class MqttClientException : Exception
    {
        public MqttClientException(string message) : base(message) {}
        public MqttClientException(string message, Exception innerException) : base(message, innerException) {}
    }
    
    public class MqttProtocolViolationException : MqttClientException
    {
        public MqttProtocolViolationException(string message) : base(message) { }

        public MqttProtocolViolationException(string message, Exception innerException) : base(message, innerException) { }
    }
}