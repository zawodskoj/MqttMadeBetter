using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zw.MqttMadeBetter.ControlPackets
{
    public class MqttControlPacketEncoder
    {
        public static async Task Encode(Stream stream, MqttControlPacket packet, byte[] reusableBuffer, CancellationToken cancellationToken)
        {
            var type = (byte) packet.Type;
            var typeFlags = packet.TypeFlags;
            var typeByte = (byte) ((type << 4) | typeFlags);

            await stream.WriteSingleByteAsync(typeByte, reusableBuffer, cancellationToken);
            
            var payload = packet switch
            {
                MqttConnectControlPacket connect => EncodeConnect(connect), 
                MqttSubscribeControlPacket subscribe => EncodeSubscribe(subscribe),
                MqttUnsubscribeControlPacket unsubscribe => EncodeUnsubscribe(unsubscribe),
                IEmptyPacket _ => Array.Empty<byte>(),
                IPacketWithOnlyId onlyId => new[] { (byte) (onlyId.PacketIdentifier >> 8), (byte) onlyId.PacketIdentifier },
                _ => throw new NotSupportedException("Not implemented yet")
            };
            
            var payloadLength = (ulong) payload.Length;
            do
            {
                var b = payloadLength & 0x7f;
                payloadLength /= 128;
                if (payloadLength > 0) b |= 0x80;

                await stream.WriteSingleByteAsync((byte) b, reusableBuffer, cancellationToken);
            } while (payloadLength > 0);

            await stream.WriteAsync(payload, cancellationToken);
        }

        private static int MeasureOrZero(string s) => s != null ? Encoding.UTF8.GetByteCount(s) + 2 : 0;

        private static byte[] EncodeConnect(MqttConnectControlPacket connect)
        {
            var measured =
                10 + // Variable header size
                MeasureOrZero(connect.ClientId) +
                MeasureOrZero(connect.Username) +
                MeasureOrZero(connect.Password) +
                (connect.WillMessage != null
                    ? MeasureOrZero(connect.WillMessage.Topic) + connect.WillMessage.Payload.Length + 2
                    : 0);

            var buffer = new byte[measured];
            
            // protocol name
            buffer[0] = 0;
            buffer[1] = 4;
            buffer[2] = (byte) 'M';
            buffer[3] = (byte) 'Q';
            buffer[4] = (byte) 'T';
            buffer[5] = (byte) 'T';
            
            // protocol level
            buffer[6] = 4;
            
            // flags
            buffer[7] = (byte) (
                (connect.Username != null ? 0x80 : 0) |
                (connect.Password != null ? 0x40 : 0) |
                (connect.WillMessage != null
                    ? (connect.WillMessage.Retain ? 0x20 : 0) |
                      ((byte) connect.WillMessage.Qos << 3) |
                      0x4
                    : 0) |
                (connect.CleanSession ? 0x2 : 0));
            
            // keep-alive
            buffer[8] = (byte) (connect.KeepAliveSeconds >> 8);
            buffer[9] = (byte) connect.KeepAliveSeconds;

            var newOffset = 10;
            newOffset = buffer.WriteUtf8StringAtOffset(connect.ClientId, newOffset);
            if (connect.WillMessage != null)
            {
                newOffset = buffer.WriteUtf8StringAtOffset(connect.WillMessage.Topic, newOffset);
                buffer[newOffset++] = (byte) (connect.WillMessage.Payload.Length / 256);
                buffer[newOffset++] = (byte) (connect.WillMessage.Payload.Length % 256);
                connect.WillMessage.Payload.CopyTo(new Memory<byte>(buffer, newOffset, connect.WillMessage.Payload.Length));
                newOffset += connect.WillMessage.Payload.Length;
            }
            if (connect.Username != null)
                newOffset = buffer.WriteUtf8StringAtOffset(connect.Username, newOffset);
            if (connect.Password != null)
                newOffset = buffer.WriteUtf8StringAtOffset(connect.Password, newOffset);

            return buffer;
        }

        private static byte[] EncodeSubscribe(MqttSubscribeControlPacket subscribe)
        {
            var measured = 2;
            
            foreach (var topicFilter in subscribe.TopicFilters)
                measured += MeasureOrZero(topicFilter.Topic) + 1;

            var buffer = new byte[measured];
            
            buffer[0] = (byte) (subscribe.PacketIdentifier / 256);
            buffer[1] = (byte) (subscribe.PacketIdentifier % 256);

            var newOffset = 2;
            foreach (var topicFilter in subscribe.TopicFilters)
            {
                newOffset = buffer.WriteUtf8StringAtOffset(topicFilter.Topic, newOffset);
                buffer[newOffset++] = (byte) topicFilter.Qos;
            }

            return buffer;
        }

        private static byte[] EncodeUnsubscribe(MqttUnsubscribeControlPacket subscribe)
        {
            var measured = 2;
            
            foreach (var topic in subscribe.Topics)
                measured += MeasureOrZero(topic);

            var buffer = new byte[measured];
            
            buffer[0] = (byte) (subscribe.PacketIdentifier / 256);
            buffer[1] = (byte) (subscribe.PacketIdentifier % 256);

            var newOffset = 2;
            foreach (var topic in subscribe.Topics)
                newOffset = buffer.WriteUtf8StringAtOffset(topic, newOffset);

            return buffer;
        }
    }
}