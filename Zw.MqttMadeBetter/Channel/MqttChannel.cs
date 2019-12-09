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
        private readonly ILogger<MqttChannel> _logger;
        private readonly Stream _stream;
        private readonly SemaphoreSlim _rdLock = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _wrLock = new SemaphoreSlim(1);
        private readonly byte[] _recvBuffer, _sendBuffer;

        private volatile bool _disposed;
        
        private MqttChannel(Socket socket, ILogger<MqttChannel> logger)
        {
            _logger = logger;
            _recvBuffer = new byte[socket.ReceiveBufferSize];
            _sendBuffer = new byte[socket.SendBufferSize];
            _stream = new DestroyableStream(socket);
        }

        public static async Task<MqttChannel> Open(MqttEndpoint endpoint, Action<Socket> configureSocket, int connectionTimeout, ILogger<MqttChannel> logger, CancellationToken cancellationToken)
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
                    var connT = sock.ConnectAsync(endpoint.Hostname, endpoint.Port);

                    var timerT = Task.Delay(connectionTimeout, cancellationToken);

                    var rT = await Task.WhenAny(connT, timerT).ConfigureAwait(false);
                    if (rT == timerT)
                    {
                        sock.Dispose();
                        throw new MqttChannelException("Failed to open channel - timed out", null);
                    }
                    else
                    {
                        await connT; // for exception
                    }

                    return new MqttChannel(sock, logger);
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
                    _logger.LogDebug("Sending packet: {Packet}", packet);
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
                    var packet = await MqttControlPacketDecoder.Decode(_stream, _recvBuffer, cancellationToken);
                    _logger.LogDebug("Packet received: {Packet}", packet);
                    return packet;
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