using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zw.MqttMadeBetter.Channel;
using Zw.MqttMadeBetter.Channel.ControlPackets;

namespace Zw.MqttMadeBetter.Client
{
    internal static class ObservableExtensions
    {
        public static async Task<T> ToAsyncTask<T>(this IObservable<T> observable, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sub = observable.Subscribe(x => tcs.TrySetResult(x), x => tcs.TrySetException(x));

            void Cancel()
            {
                // ReSharper disable once AccessToDisposedClosure
                sub.Dispose();
                tcs.TrySetCanceled();
            }
            
            await using (cancellationToken.Register(Cancel))
            {
                try
                {
                    return await tcs.Task;
                }
                finally
                {
                    sub.Dispose();
                }
            }
        }

        private static Task<T> WaitSpecificMessage<T>(this IObservable<MqttControlPacket> observable, Func<T, bool> filter, CancellationToken cancellationToken)
            where T : MqttControlPacket
            => filter != null
                ? observable
                    .OfType<T>()
                    .FirstAsync(filter)
                    .ToAsyncTask(cancellationToken)
                : observable
                    .OfType<T>()
                    .FirstAsync()
                    .ToAsyncTask(cancellationToken);

        public static async Task<T> WaitSpecificMessage<T>(this IObservable<MqttControlPacket> observable, Func<T, bool> filter, int timeoutMs, CancellationToken cancellationToken)
            where T : MqttControlPacket
        {
            var task = observable.WaitSpecificMessage(filter, cancellationToken);

            if (await Task.WhenAny(task, Task.Delay(timeoutMs, cancellationToken)) != task)
                return null;

            return await task;
        }
    }

    
    public class MqttClient : IDisposable
    {
        private readonly MqttChannel _channel;
        private readonly MqttReadOnlyClientOptions _options;
        private readonly int _ackTimeoutMs;
        private readonly CancellationTokenSource _globalCts = new CancellationTokenSource();

        private readonly IObservable<MqttControlPacket> _packets;
        private event Action<MqttControlPacket> PacketsEv;

        private volatile int _packetIdCounter;
        private volatile int _disposedFlag;
        private volatile bool _disconnecting;

        private MqttClient(MqttChannel channel, MqttReadOnlyClientOptions options)
        {
            const int defaultAckTimeoutToKeepAliveRatio = 3;
            
            _channel = channel;
            _options = options;
            _ackTimeoutMs = options.ChannelOptions.AcknowledgeTimeout ??
                            options.ConnectionOptions.KeepAliveSeconds * 1000 / defaultAckTimeoutToKeepAliveRatio;
            
            _packets = Observable.FromEvent<MqttControlPacket>(x => PacketsEv += x, x => PacketsEv -= x,
                Scheduler.Immediate);
            Messages = _packets.OfType<MqttPublishControlPacket>().Select(HandleMessage);
        }

        public static async Task<MqttClient> Create(MqttReadOnlyClientOptions options, CancellationToken cancellationToken)
        {
            var client = new MqttClient(
                await MqttChannel.Open(options.Endpoint.Hostname, options.Endpoint.Port, socket =>
                {
                    var keepAlive = options.ConnectionOptions.KeepAliveSeconds * 1000;
                    var chanOpts = options.ChannelOptions;

                    socket.SendTimeout = chanOpts.SendTimeout ?? keepAlive;
                    socket.ReceiveTimeout = chanOpts.ReceiveTimeout ?? keepAlive;
                    
                    chanOpts.CustomSocketConfig?.Invoke(socket);
                }, cancellationToken), 
                options);
            
            await client.Init(options.ConnectionOptions, cancellationToken);
            
            return client;
        }

        private static string TranslateConnackReturnCode(MqttConnackReturnCode returnCode) => returnCode switch
        { 
            MqttConnackReturnCode.ACCEPTED => "Connection accepted",
            MqttConnackReturnCode.NOT_AUTHORIZED => "Unauthorized",
            MqttConnackReturnCode.BAD_CREDENTIALS => "Bad username  or password",
            MqttConnackReturnCode.SERVER_UNAVAILABLE => "Server is unavailable",
            MqttConnackReturnCode.IDENTIFIER_REJECTED => "Client identifier is not allowed",
            MqttConnackReturnCode.UNACCEPTABLE_PROTOCOL => "Protocol is not supported",
            _ => "Invalid return code: " + returnCode
        };
        
        private async Task Init(MqttReadOnlyConnectionOptions options, CancellationToken cancellationToken)
        {
            try
            {
                var packet = new MqttConnectControlPacket(
                    options.ClientId,
                    options.Credentials?.Username,
                    options.Credentials?.Password,
                    options.WillMessage,
                    options.CleanSession,
                    options.KeepAliveSeconds);
                
                await _channel.Send(packet, cancellationToken);
                var connack = await _channel.Receive(cancellationToken);
                if (!(connack is MqttConnackControlPacket realConnack))
                    throw Error("Unexpected packet received, expected CONNACK, got " + connack.Type);

                if (realConnack.ReturnCode != MqttConnackReturnCode.ACCEPTED)
                    throw Error(TranslateConnackReturnCode(realConnack.ReturnCode));

                _ = StartReceiving();
                _ = StartPing();
            }
            catch (MqttClientException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw Error("Failed to initiate connection", e);
            }
        }

        private async Task StartReceiving()
        {
            while (!_globalCts.IsCancellationRequested)
            {
                try
                {
                    var received = await _channel.Receive(_globalCts.Token);
                    Console.WriteLine("Received control packet " + received);
                    PacketsEv?.Invoke(received);
                    Console.WriteLine("Processed control packet " + received);
                }
                catch (Exception e)
                {
                    throw Error("Failed to receive packet", e);
                }
            }
        }

        private async Task StartPing()
        {
            const int defaultPingIntervalToKeepAliveRatio = 2;
            
            var pingInterval = _options.ChannelOptions.PingInterval ?? 
                               _options.ConnectionOptions.KeepAliveSeconds * 1000 / defaultPingIntervalToKeepAliveRatio;
            
            var stopwatch = new Stopwatch();
            var token = _globalCts.Token;
            
            while (!token.IsCancellationRequested)
            {
                var waiter = _packets.WaitSpecificMessage<MqttPingrespControlPacket>(_ => true, pingInterval, token);
                await Send(new MqttPingreqControlPacket(), token);

                stopwatch.Restart();
                
                if (await Task.WhenAny(waiter, Task.Delay(pingInterval, token)) != waiter)
                    throw Violation("No ping response received");

                var elapsed = stopwatch.ElapsedMilliseconds;
                await Task.Delay((int) Math.Max(0, pingInterval - elapsed), token);
            }
        }
        
        public IObservable<MqttMessage> Messages { get; }
        public event EventHandler<MqttClientException> ConnectionClosed;

        private MqttClientException Error(string s, Exception e = null)
        {
            var exception = e != null ? new MqttClientException(s, e) : new MqttClientException(s);
            Dispose(exception);
            
            return exception;
        }
        
        private MqttProtocolViolationException Violation(string s, Exception e = null)
        {
            var exception = e != null ? new MqttProtocolViolationException(s, e) : new MqttProtocolViolationException(s);
            Dispose(exception);
            
            return exception;
        }

        private ushort AllocatePacketId()
        {
            return (ushort) (1 + Interlocked.Increment(ref _packetIdCounter) % 0xffff);
        }

        private async Task Send(MqttControlPacket packet, CancellationToken token)
        {
            try
            {
                await _channel.Send(packet, token);
            }
            catch (Exception e)
            {
                throw Error("Failed to send packet", e);
            }
        }

        private async Task<T> SendAndReceive<T>(MqttControlPacketWithId packet, CancellationToken token) where T : MqttControlPacketWithId
        {
            var waiter = _packets.WaitSpecificMessage<T>(x => x.PacketIdentifier == packet.PacketIdentifier, _ackTimeoutMs, token);

            await Send(packet, token);
            return await waiter ?? throw Violation($"Expected {typeof(T).Name}, did not receive in {_ackTimeoutMs} ms, closing connection");
        }

        public async Task Send(string topic, MqttMessageQos qos, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var packetId = AllocatePacketId();
            var packet = new MqttPublishControlPacket(false, qos, false, topic, packetId, payload);
            
            switch (qos)
            {
                case MqttMessageQos.QOS_0:
                    await Send(packet, cancellationToken);
                    break;
                case MqttMessageQos.QOS_1:
                    await SendAndReceive<MqttPubackControlPacket>(packet, cancellationToken);
                    break;
                case MqttMessageQos.QOS_2:
                    await SendAndReceive<MqttPubrecControlPacket>(packet, cancellationToken);
                    var pubrel = new MqttPubrelControlPacket(packetId);
                    await SendAndReceive<MqttPubcompControlPacket>(pubrel, cancellationToken);
                    break;
            }
        }

        public async Task Acknowledge(MqttMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            switch (message.Qos)
            {
                case MqttMessageQos.QOS_0:
                    return;
                case MqttMessageQos.QOS_1:
                    await Send(new MqttPubackControlPacket(message.PacketIdentifier), cancellationToken);
                    return;
                case MqttMessageQos.QOS_2:
                    await SendAndReceive<MqttPubrelControlPacket>(
                        new MqttPubrecControlPacket(message.PacketIdentifier), cancellationToken);
                    await Send(new MqttPubcompControlPacket(message.PacketIdentifier), cancellationToken);
                    
                    return;
            }
        }

        public async Task Subscribe(string topic, MqttMessageQos qos, CancellationToken cancellationToken)
        {
            var subscribe = new MqttSubscribeControlPacket(AllocatePacketId(), new[] {new TopicFilter(topic, qos)});
            var suback = await SendAndReceive<MqttSubackControlPacket>(subscribe, cancellationToken);
            if (suback.Results[0] == SubackResultCode.FAILURE)
                throw Violation("Failed to subscribe to topic " + topic);
        }
        
        public async Task Unsubscribe(string topic, CancellationToken cancellationToken)
        {
            var subscribe = new MqttUnsubscribeControlPacket(AllocatePacketId(), new[] { topic });
            await SendAndReceive<MqttUnsubackControlPacket>(subscribe, cancellationToken);
        }
        
        public async Task Disconnect(bool throwOnUngracefulDisconnection, CancellationToken cancellationToken)
        {
            _disconnecting = true;

            try
            {
                await Send(new MqttDisconnectControlPacket(), cancellationToken);
            }
            catch
            {
                if (throwOnUngracefulDisconnection)
                    throw;
            }
            finally
            {
                Dispose();
            }
        }
        
        private MqttMessage HandleMessage(MqttPublishControlPacket packet)
        {
            var message = new MqttMessage(packet);

            if (_options.AutoAcknowledge)
                _ = Acknowledge(message, _globalCts.Token);

            return message;
        }

        private void Dispose(MqttClientException e)
        {
            if (Interlocked.CompareExchange(ref _disposedFlag, 1, 0) != 0)
                return;
            
            _globalCts.Cancel();
            _globalCts.Dispose();
            _channel.Dispose();

            ConnectionClosed?.Invoke(this, _disconnecting ? null : e);
        }

        public void Dispose()
        {
            Dispose(null);
        }
    }
}