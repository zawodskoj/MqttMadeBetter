using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zw.MqttMadeBetter.ControlPackets
{
    internal struct FixedHeaderWithRemainingLength
    {
        public FixedHeaderWithRemainingLength(MqttControlPacketType type, byte typeFlags, long remainingLength)
        {
            Type = type;
            TypeFlags = typeFlags;
            RemainingLength = remainingLength;
        }

        public MqttControlPacketType Type { get; }
        public byte TypeFlags { get; }
        public long RemainingLength { get; }
    }
    
    internal static class MqttStreamHelpers
    {
        public static async Task<byte> ReadSingleByteAsync(this Stream stream, byte[] reusableBuffer, CancellationToken cancellationToken)
        {
            if (await stream.ReadAsync(reusableBuffer, 0, 1, cancellationToken) == 0)
                throw new EndOfStreamException();

            return reusableBuffer[0];
        }
        
        public static async Task WriteSingleByteAsync(this Stream stream, byte b, byte[] reusableBuffer, CancellationToken cancellationToken)
        {
            reusableBuffer[0] = b;
            await stream.WriteAsync(reusableBuffer, 0, 1, cancellationToken);
        }

        public static async Task ReadFullAsync(this Stream stream, Memory<byte> memory,
            CancellationToken cancellationToken)
        {
            var totalLength = memory.Length;
            if (totalLength == 0)
                return;
            
            var totalRead = 0;

            do
            {
                var read = await stream.ReadAsync(memory, cancellationToken);
                if (read == 0) throw new EndOfStreamException();

                totalRead += read;
                memory = memory.Slice(read);
            } while (totalRead < totalLength);
        }

        public static (string String, int NextOffset) ReadUtf8StringAtOffset(this byte[] array, int offset)
        {
            if (array.Length - 1 < offset || offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            if (array.Length - 2 < offset)
                throw new ArgumentException("Not enough space for string length", nameof(array));

            var length = array[offset] * 256 + array[offset + 1];
            if (array.Length - length - 2 < offset)
                throw new ArgumentException($"Not enough space for string with length {length} - only {array.Length - offset - 2} bytes available", nameof(array));

            var nextOffset = offset + 2 + length;
            var str = Encoding.UTF8.GetString(array, offset + 2, length);

            return (str, nextOffset);
        }
        
        public static int WriteUtf8StringAtOffset(this byte[] array, string s, int offset)
        {
            if (array.Length - 1 < offset || offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            if (array.Length - 2 < offset)
                throw new ArgumentException("Not enough space for string length", nameof(array));

            var length = Encoding.UTF8.GetByteCount(s);
            if (length > 0xffff)
                throw new ArgumentException($"Length limit reached - max size 65536 bytes", nameof(s));
            
            array[offset] = (byte) (length / 256);
            array[offset + 1] = (byte) (length % 256);
            
            if (array.Length - length - 2 < offset)
                throw new ArgumentException($"Not enough space for string with length {length} - only {array.Length - offset - 2} bytes available", nameof(array));

            var nextOffset = offset + 2 + length;
            Encoding.UTF8.GetBytes(s, 0, s.Length, array, offset + 2);

            return nextOffset;
        }
    }
}