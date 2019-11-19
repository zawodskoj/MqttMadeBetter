using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Zw.MqttMadeBetter
{
    internal class DestroyableStream : Stream
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;

        public DestroyableStream(TcpClient client)
        {
            _client = client;
            _stream = client.GetStream();
        }

        public override void Close()
        {
            _stream.Close();
            _client.Close();
        }

        public override void CopyTo(Stream destination, int bufferSize) => 
            _stream.CopyTo(destination, bufferSize);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(Dispose))
                return _stream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
                _client.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await _stream.DisposeAsync();
            _client.Dispose();
        }

        public override void Flush() => 
            _stream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(Dispose))
                return _stream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int size) => 
            _stream.Read(buffer, offset, size);

        public override int Read(Span<byte> buffer) => 
            _stream.Read(buffer);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(Dispose))
                return _stream.ReadAsync(buffer, offset, size, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            using (cancellationToken.Register(Dispose))
                return _stream.ReadAsync(buffer, cancellationToken);
        }

        public override int ReadByte() => 
            _stream.ReadByte();

        public override long Seek(long offset, SeekOrigin origin) => 
            _stream.Seek(offset, origin);

        public override void SetLength(long value) => 
            _stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int size) => 
            _stream.Write(buffer, offset, size);

        public override void Write(ReadOnlySpan<byte> buffer) => 
            _stream.Write(buffer);

        public override Task WriteAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(Dispose))
                return _stream.WriteAsync(buffer, offset, size, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            using (cancellationToken.Register(Dispose))
                return _stream.WriteAsync(buffer, cancellationToken);
        }

        public override void WriteByte(byte value) => 
            _stream.WriteByte(value);

        public override bool CanRead => _stream.CanRead;

        public override bool CanSeek => _stream.CanSeek;

        public override bool CanTimeout => _stream.CanTimeout;

        public override bool CanWrite => _stream.CanWrite;

        public override long Length => _stream.Length;

        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        public override int ReadTimeout
        {
            get => _stream.ReadTimeout;
            set => _stream.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => _stream.WriteTimeout;
            set => _stream.WriteTimeout = value;
        }
    }
}