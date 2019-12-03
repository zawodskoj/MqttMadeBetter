using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;

namespace Zw.MqttMadeBetter.Channel.ControlPackets
{
    public class MqttControlPacketDecoder
    {
        private delegate MqttControlPacket Decoder(ReadOnlySpan<byte> payload, byte typeFlags);

        private static readonly Decoder[] Decoders =
        {
            Reserved, // Reserved type
            Unsupported, // CONNECT is not yet implemented
            DecodeConnack,
            DecodePublish,
            DecodeOnlyId<MqttPubackControlPacketFactory>,
            DecodeOnlyId<MqttPubrecControlPacketFactory>,
            DecodeOnlyId<MqttPubrelControlPacketFactory>,
            DecodeOnlyId<MqttPubcompControlPacketFactory>,
            Unsupported, // SUBSCRIBE is not yet implemented 
            DecodeSuback,
            Unsupported, // UNSUBSCRIBE is not yet implemented
            DecodeOnlyId<MqttUnsubackControlPacketFactory>,
            DecodeEmpty<MqttPingreqControlPacket>,
            DecodeEmpty<MqttPingrespControlPacket>,
            DecodeEmpty<MqttDisconnectControlPacket>,
            Reserved // Reserved type
        };
        
        public static bool TryDecode(PipeReader reader, CancellationToken cancellationToken, out MqttControlPacket packet)
        {
            packet = null;
            
            if (!reader.TryRead(out var result) || result.Buffer.Length < 2)
                return false;

            var buffer = result.Buffer;
           
            var typeByte = buffer.FirstSpan[0];
            var type = typeByte >> 4;
            var typeFlags = (byte) (typeByte & 0xf);

            var payloadLength = 0L;
            var multiplier = 1L;

            var readCompletely = false;

            var bufferWithoutTypeByte = buffer.Slice(1);
            var offset = 0;

            foreach (var piece in bufferWithoutTypeByte)
            {
                foreach (var payloadLenByte in piece.Span)
                {
                    payloadLength += multiplier * (payloadLenByte & 0x7f);
                    multiplier *= 128;

                    offset++;

                    if ((payloadLenByte & 0x80) == 0)
                    {
                        readCompletely = true;
                        goto lenRead;
                    }
                }
            }

            lenRead: 
            
            if (!readCompletely)
                return false;

            var payloadPart = bufferWithoutTypeByte.Slice(offset);
            if (payloadPart.Length < payloadLength)
                return false;
            
            // todo: do not allocate
            var memory = new Memory<byte>(new byte[payloadLength]);
            var curSlice = memory;
            
            foreach (var piece in payloadPart)
            {
                piece.CopyTo(curSlice);
                curSlice = memory.Slice(piece.Length);
                
                if (curSlice.Length == 0) break;
            }
            
            var decoder = Decoders[type];
            
            packet = decoder(memory.Span, typeFlags);
            reader.AdvanceTo(buffer.GetPosition(1 + offset + payloadLength));
            return true;
        }

        private static MqttControlPacket Unsupported(ReadOnlySpan<byte> payload, byte typeFlags) 
            => throw new NotSupportedException("Not implemented yet");
        
        private static MqttControlPacket Reserved(ReadOnlySpan<byte> payload, byte typeFlags) 
            => throw new NotSupportedException("Reserved control packet type - can't decode");
        
        
        private static MqttControlPacket DecodeConnack(ReadOnlySpan<byte> payload, byte typeFlags)
        {
            if (payload.Length != 2) 
                throw new Exception("Invalid payload (should be 2 bytes long)");

            return new MqttConnackControlPacket((payload[0] & 1) == 1, (MqttConnackReturnCode) payload[1]);
        }
        
        private static MqttControlPacket DecodePublish(ReadOnlySpan<byte> payload, byte typeFlags)
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
                payload.Slice(nextOffset).ToArray());
        }

        private static MqttControlPacket DecodeSuback(ReadOnlySpan<byte> payload, byte typeFlags)
        {
            var packetIdentifier = (ushort) (payload[0] * 256 + payload[1]);

            var results = new SubackResultCode[payload.Length - 2];
            for (var i = 0; i < results.Length; i++)
                results[i] = (SubackResultCode) payload[i + 2];

            return new MqttSubackControlPacket(packetIdentifier, results);
        }

        private static MqttControlPacket DecodeOnlyId<TFac>(ReadOnlySpan<byte> payload, byte typeFlags) where TFac : struct, IPacketWithOnlyIdFactory
        {
            if (payload.Length != 2)
                throw new InvalidDataException("Expected two bytes payload, got payload with length " + payload.Length);

            var packetIdentifier = (ushort) (payload[0] * 256 + payload[1]);

            return default(TFac).Create(packetIdentifier);
        }
        
        private static MqttControlPacket DecodeEmpty<T>(ReadOnlySpan<byte> payload, byte typeFlags) where T : MqttControlPacket, new()
        {
            if (payload.Length != 0)
                throw new InvalidDataException("Expected no payload, got payload with length " + payload.Length);
            
            return new T();
        }
    }
}