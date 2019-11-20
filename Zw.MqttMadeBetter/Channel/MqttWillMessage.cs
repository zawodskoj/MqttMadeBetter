using System;
using Zw.MqttMadeBetter.Channel.ControlPackets;

namespace Zw.MqttMadeBetter.Channel
{
    public class MqttWillMessage
    {
        public MqttMessageQos Qos { get; }
        public bool Retain { get; }
        public string Topic { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public MqttWillMessage(MqttMessageQos qos, bool retain, string topic, ReadOnlyMemory<byte> payload)
        {
            Qos = qos;
            Retain = retain;
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
            Payload = payload;
            
            if (payload.Length > 65535)
                throw new ArgumentException("Payload should be smaller than 65536 bytes", nameof(payload));
        }
    }
}