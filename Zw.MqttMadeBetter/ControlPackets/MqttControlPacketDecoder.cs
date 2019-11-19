using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zw.MqttMadeBetter.ControlPackets
{
    public class MqttControlPacketDecoder
    {
        private delegate MqttControlPacket Decoder(byte[] payload, byte typeFlags);

        private static readonly Decoder[] Decoders =
        {
            Reserved, // Reserved type
            Unsupported, // CONNECT is not yet implemented
            DecodeConnack,
            DecodePublish,
            Unsupported, // PUBACK is not yet implemented 
            Unsupported, // PUBREC is not yet implemented 
            Unsupported, // PUBREL is not yet implemented 
            Unsupported, // PUBCOMP is not yet implemented
            Unsupported, // SUBSCRIBE is not yet implemented 
            DecodeSuback,
            Unsupported, // UNSUBSCRIBE is not yet implemented
            DecodeUnsuback,
            DecodePingreq,
            DecodePingresp,
            Unsupported, // DISCONNECT is not yet implemented,
            Reserved // Reserved type
        };
        
        public static async Task<MqttControlPacket> Decode(Stream stream, byte[] reusableBuffer, CancellationToken cancellationToken)
        {
            var typeByte = await stream.ReadSingleByteAsync(reusableBuffer, cancellationToken);
            var type = typeByte >> 4;
            var typeFlags = (byte) (typeByte & 0xf);

            var payloadLength = 0UL;
            byte payloadLenByte;

            do
            {
                payloadLenByte = await stream.ReadSingleByteAsync(reusableBuffer, cancellationToken);
                payloadLength *= 128;
                payloadLength += (ulong) (payloadLenByte & 0x7f);
            } while ((payloadLenByte & 0x80) > 0);

            var payload = new byte[payloadLength];
            await stream.ReadFullAsync(payload, cancellationToken);
            
            var decoder = Decoders[type];
            return decoder(payload, typeFlags);
        }

        private static MqttControlPacket Unsupported(byte[] payload, byte typeFlags) 
            => throw new NotSupportedException("Not implemented yet");
        
        private static MqttControlPacket Reserved(byte[] payload, byte typeFlags) 
            => throw new NotSupportedException("Reserved control packet type - can't decode");
        
        
        private static MqttControlPacket DecodeConnack(byte[] payload, byte typeFlags)
        {
            if (payload.Length != 2) 
                throw new Exception("Invalid payload (should be 2 bytes long)");

            return new MqttConnackControlPacket((payload[0] & 1) == 1, (MqttConnackReturnCode) payload[1]);
        }
        
        private static MqttControlPacket DecodePublish(byte[] payload, byte typeFlags)
        {
            var qos = (MqttMessageQos) ((typeFlags >> 1) & 0x3);
            
            var (topicName, nextOffset) = payload.ReadUtf8StringAtOffset(0);

            ushort packetIdentifier = 0;
            if (qos != MqttMessageQos.QOS_0)
            {
                packetIdentifier = (ushort) (payload[nextOffset] * 256 + payload[nextOffset + 1]);
                nextOffset += 2;
            }

            return new MqttPublishControlPacket(
                (typeFlags & 0x8) == 0x8,
                qos,
                (typeFlags & 0x1) == 0x1,
                topicName,
                packetIdentifier,
                payload.AsMemory(nextOffset));
        }

        private static MqttControlPacket DecodeSuback(byte[] payload, byte typeFlags)
        {
            var packetIdentifier = (ushort) (payload[0] * 256 + payload[1]);

            var results = new SubackResultCode[payload.Length - 2];
            for (var i = 0; i < results.Length; i++)
                results[i] = (SubackResultCode) payload[i + 2];

            return new MqttSubackControlPacket(packetIdentifier, results);
        }

        private static MqttControlPacket DecodeUnsuback(byte[] payload, byte typeFlags)
        {
            var packetIdentifier = (ushort) (payload[0] * 256 + payload[1]);

            return new MqttUnsubackControlPacket(packetIdentifier);
        }

        private static MqttControlPacket DecodePingreq(byte[] payload, byte typeFlags)
        {
            return new MqttPingreqControlPacket();
        }

        private static MqttControlPacket DecodePingresp(byte[] payload, byte typeFlags)
        {
            return new MqttPingrespControlPacket();
        }
    }
}