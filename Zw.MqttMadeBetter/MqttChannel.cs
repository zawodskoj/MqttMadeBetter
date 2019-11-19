using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Zw.MqttMadeBetter.ControlPackets;

namespace Zw.MqttMadeBetter
{
    public class MqttChannel : IDisposable
    {
        private readonly Stream _stream;
        private readonly SemaphoreSlim _rdLock = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _wrLock = new SemaphoreSlim(1);
        private readonly byte[] _recvBuffer, _sendBuffer;

        private MqttChannel(TcpClient tcp)
        {
            _recvBuffer = new byte[tcp.ReceiveBufferSize];
            _sendBuffer = new byte[tcp.SendBufferSize];
            _stream = new DestroyableStream(tcp);
        }

        public static async Task<MqttChannel> Open(string hostname, int port, Action<TcpClient> configureTcp, CancellationToken cancellationToken)
        {
            if (hostname == null)
                throw new ArgumentNullException(nameof(hostname));

            var tcp = new TcpClient
            {
                LingerState = new LingerOption(false, 0),
                NoDelay = true
            };
            
            if (configureTcp == null)
                throw new ArgumentNullException(nameof(configureTcp));

            configureTcp(tcp);

            cancellationToken.ThrowIfCancellationRequested();

            await using (cancellationToken.Register(tcp.Dispose))
            {
                try
                {
                    await tcp.ConnectAsync(hostname, port);
                    
                    return new MqttChannel(tcp);
                }
                catch (ObjectDisposedException e)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException();

                    throw new MqttChannelException("Failed to open channel", e);
                }
                catch (Exception e)
                {
                    tcp.Dispose();
                    throw new MqttChannelException("Failed to open channel", e);
                }
            }
        }
        
        public async Task Send(MqttControlPacket packet, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await using (cancellationToken.Register(Dispose))
            {
                await _wrLock.WaitAsync(cancellationToken);

                try
                {
                    await MqttControlPacketEncoder.Encode(_stream, packet, _sendBuffer, cancellationToken);
                }
                catch (Exception e)
                {
                    Dispose();
                    throw new MqttChannelException("Failed to send a packet", e);
                }
                finally
                {
                    _wrLock.Release();
                }
            }
        }

        public async Task<MqttControlPacket> Receive(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            await using (cancellationToken.Register(Dispose))
            {
                await _rdLock.WaitAsync(cancellationToken);

                try
                {
                    return await MqttControlPacketDecoder.Decode(_stream, _recvBuffer, cancellationToken);
                }
                catch (Exception e)
                {
                    Dispose();
                    throw new MqttChannelException("Failed to receive a packet", e);
                }
                finally
                {
                    _rdLock.Release();
                }
            }
        }

        public void Dispose()
        {
            _stream?.Dispose();
        }
    }
}