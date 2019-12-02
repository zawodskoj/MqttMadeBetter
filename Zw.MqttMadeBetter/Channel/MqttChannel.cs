using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Zw.MqttMadeBetter.Channel.ControlPackets;
using Zw.MqttMadeBetter.Client;

namespace Zw.MqttMadeBetter.Channel
{
    public class MqttChannel : IDisposable
    {
        private readonly Stream _stream;
        private readonly SemaphoreSlim _rdLock = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _wrLock = new SemaphoreSlim(1);
        private readonly byte[] _recvBuffer, _sendBuffer;

        private volatile bool _disposed;
        
        private MqttChannel(Socket socket)
        {
            _recvBuffer = new byte[socket.ReceiveBufferSize];
            _sendBuffer = new byte[socket.SendBufferSize];
            _stream = new DestroyableStream(socket);
        }

        public static async Task<MqttChannel> Open(MqttEndpoint endpoint, Action<Socket> configureSocket, ILogger<MqttChannel> logger, CancellationToken cancellationToken)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            var sock = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                LingerState = new LingerOption(false, 0),
                NoDelay = true,
            };
            configureSocket?.Invoke(sock);
            
            cancellationToken.ThrowIfCancellationRequested();

            await using (cancellationToken.Register(sock.Dispose))
            {
                try
                {
                    await sock.ConnectAsync(endpoint.Hostname, endpoint.Port).ConfigureAwait(false);

                    return new MqttChannel(sock);
                }
                catch (ObjectDisposedException e)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException();

                    throw new MqttChannelException("Failed to open channel", e);
                }
                catch (Exception e)
                {
                    sock.Dispose();
                    throw new MqttChannelException("Failed to open channel", e);
                }
            }
        }
        
        public async Task Send(MqttControlPacket packet, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MqttChannel));
            cancellationToken.ThrowIfCancellationRequested();
            
            await using (cancellationToken.Register(Dispose))
            using (await _wrLock.Enter(cancellationToken))
            {
                try
                {
                    await MqttControlPacketEncoder.Encode(_stream, packet, _sendBuffer, cancellationToken);
                }
                catch (Exception e)
                {
                    Dispose();
                    throw new MqttChannelException("Failed to send a packet", e);
                }
            }
        }

        public async Task<MqttControlPacket> Receive(CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MqttChannel));
            cancellationToken.ThrowIfCancellationRequested();
            
            await using (cancellationToken.Register(Dispose))
            using (await _rdLock.Enter(cancellationToken))
            {
                try
                {
                    return await MqttControlPacketDecoder.Decode(_stream, _recvBuffer, cancellationToken);
                }
                catch (Exception e)
                {
                    Dispose();
                    throw new MqttChannelException("Failed to receive a packet", e);
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _stream?.Dispose();
        }
    }
}