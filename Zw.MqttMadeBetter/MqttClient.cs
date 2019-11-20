using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zw.MqttMadeBetter.ControlPackets;

namespace Zw.MqttMadeBetter
{
    public class MqttMessage
    {
        internal MqttMessage(MqttPublishControlPacket packet)
        {
            IsDuplicate = packet.IsDuplicate;
            Qos = packet.Qos;
            TopicName = packet.TopicName;
            Payload = packet.Payload;
            PacketIdentifier = packet.PacketIdentifier;
        }

        public bool IsDuplicate { get; }
        public MqttMessageQos Qos { get; }
        public string TopicName { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        
        internal ushort PacketIdentifier { get; }
    }
    
    public class MqttConnectionOptions
    {
        public string Hostname { get; set; }
        public int Port { get; set; }
        public string ClientId { get; set; }
        public bool AutoAcknowledge { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public MqttWillMessage WillMessage { get; set; }
        public bool CleanSession { get; set; }
        public ushort KeepAliveSeconds { get; set; }

        internal MqttConnectControlPacket BuildPacket()
        {
            return new MqttConnectControlPacket(
                ClientId, Username, Password, WillMessage, CleanSession, KeepAliveSeconds);
        }
    }

    public class MqttClientException : Exception
    {
        public MqttClientException(string message) : base(message) {}
        public MqttClientException(string message, Exception innerException) : base(message, innerException) {}
    }

    public static class ObservableExtensions
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
    }
    
    public class MqttClient : IDisposable
    {
        private readonly MqttChannel _channel;
        private readonly ushort _keepAliveSeconds;
        private readonly bool _autoAck;
        private readonly int _redeliveryIntervalMs = 500;
        private readonly int _discardTimeoutMs;
        private volatile int _packetIdCntr;
        private readonly CancellationTokenSource _globalCts = new CancellationTokenSource();
        private readonly IObservable<MqttControlPacket> _packets;
        private event Action<MqttControlPacket> _packetsEv;

        private MqttClient(MqttChannel channel, ushort keepAliveSeconds, bool autoAck)
        {
            _channel = channel;
            _keepAliveSeconds = keepAliveSeconds;
            _autoAck = autoAck;
            _discardTimeoutMs = keepAliveSeconds == 0 ? 500 : keepAliveSeconds * 500;
            
            _packets = Observable.FromEvent<MqttControlPacket>(x => _packetsEv += x, x => _packetsEv -= x,
                Scheduler.Immediate);
            Messages = _packets.OfType<MqttPublishControlPacket>().Select(HandleMessage);
        }

        public static async Task<MqttClient> Create(MqttConnectionOptions options, CancellationToken cancellationToken)
        {
            var packet = options.BuildPacket();
            
            var client = new MqttClient(
                await MqttChannel.Open(options.Hostname, options.Port, tcp =>
                {
                    if (options.KeepAliveSeconds != 0)
                    {
                        tcp.SendTimeout = options.KeepAliveSeconds * 1500;
                        tcp.ReceiveTimeout = options.KeepAliveSeconds * 1500;
                    }
                }, cancellationToken), 
                packet.KeepAliveSeconds,
                options.AutoAcknowledge);
            
            await client.Init(packet, cancellationToken);
            
            return client;
        }

        private static string TranslateConnackReturnCode(MqttConnackReturnCode returnCode) => returnCode switch
        { 
            MqttConnackReturnCode.ACCEPTED => "Connection accepted",
            MqttConnackReturnCode.NOT_AUTHORIZED => "Unauthorized",
            MqttConnackReturnCode.BAD_CREDENTIALS => "Bad username or password",
            MqttConnackReturnCode.SERVER_UNAVAILABLE => "Server is unavailable",
            MqttConnackReturnCode.IDENTIFIER_REJECTED => "Client identifier is not allowed",
            MqttConnackReturnCode.UNACCEPTABLE_PROTOCOL => "Protocol is not supported"
        };
        
        private async Task Init(MqttConnectControlPacket packet, CancellationToken cancellationToken)
        {
            void DisposeAndThrow(Exception e)
            {
                _channel.Dispose();
                throw e;
            }
            
            await _channel.Send(packet, cancellationToken);
            var connack = await _channel.Receive(cancellationToken);
            if (!(connack is MqttConnackControlPacket realConnack))
                DisposeAndThrow(new MqttClientException("Unexpected packet received, expected CONNACK, got " + connack.Type));
            
            StartReceiving();
            
            if (realConnack.ReturnCode != MqttConnackReturnCode.ACCEPTED)
                DisposeAndThrow(new MqttClientException(TranslateConnackReturnCode(realConnack.ReturnCode)));

            StartPing();
        }

        private async Task StartReceiving()
        {
            while (!_globalCts.IsCancellationRequested)
            {
                try
                {
                    var received = await _channel.Receive(_globalCts.Token);
                    Console.WriteLine("Received control packet " + received);
                    _packetsEv?.Invoke(received);
                    Console.WriteLine("Processed control packet " + received);
                }
                catch (Exception e)
                {
                    
                }
            }
        }

        private async Task StartPing()
        {
            const int defaultPingInterval = 5000;
            
            var pingInterval = _keepAliveSeconds == 0 ? defaultPingInterval : _keepAliveSeconds * 500;

            var stopwatch = new Stopwatch();
            
            while (!_globalCts.IsCancellationRequested)
            {
                var pingWaiter = _packets.OfType<MqttPingrespControlPacket>().FirstAsync().ToAsyncTask(_globalCts.Token);
                await _channel.Send(new MqttPingreqControlPacket(), _globalCts.Token);

                stopwatch.Restart();
                
                if (await Task.WhenAny(pingWaiter, Task.Delay(pingInterval, _globalCts.Token)) != pingWaiter)
                {
                    // No ping received
                    // TODO: notify about error

                    Dispose();
                    return;
                }

                var elapsed = stopwatch.ElapsedMilliseconds;
                Console.WriteLine("Ping: {0} ms", elapsed);
                await Task.Delay((int) Math.Max(0, pingInterval - elapsed), _globalCts.Token);
            }
        }
        
        public IObservable<MqttMessage> Messages { get; }

        private Task<T> WaitSpecificMessage<T>(Func<T, bool> filter, CancellationToken cancellationToken)
            where T : MqttControlPacket
            => filter != null
                ? _packets
                    .OfType<T>()
                    .FirstAsync(filter)
                    .ToAsyncTask(cancellationToken)
                : _packets
                    .OfType<T>()
                    .FirstAsync()
                    .ToAsyncTask(cancellationToken);

        private async Task<T> WaitSpecificMessage<T>(Func<T, bool> filter, int timeoutMs, CancellationToken cancellationToken)
            where T : MqttControlPacket
        {
            var task = WaitSpecificMessage(filter, cancellationToken);

            if (await Task.WhenAny(task, Task.Delay(timeoutMs, cancellationToken)) != task)
                return null;

            return await task;
        }

        private async Task RetryUntilSpecificMessageReceived<T>(Func<T, bool> filter, Func<bool, Task> action, int retryTimeoutMs, CancellationToken cancellationToken)
            where T : MqttControlPacket
        {
            var waiter = WaitSpecificMessage(filter, cancellationToken);

            var retry = false;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                await action(retry);
                
                if (await Task.WhenAny(waiter, Task.Delay(retryTimeoutMs, cancellationToken)) == waiter)
                {
                    return;
                }

                retry = true;
            }
        }

        private ushort AllocatePacketId()
        {
            return (ushort) (1 + Interlocked.Increment(ref _packetIdCntr) % 0xffff);
        }

        public async Task Send(string topic, MqttMessageQos qos, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var packetId = AllocatePacketId();
            
            switch (qos)
            {
                case MqttMessageQos.QOS_0:
                {
                    var packet = new MqttPublishControlPacket(false, qos, false, topic, packetId, payload);
                    await _channel.Send(packet, cancellationToken);
                    break;
                }
                case MqttMessageQos.QOS_1:
                    await RetryUntilSpecificMessageReceived<MqttPubackControlPacket>(
                        x => x.PacketIdentifier == packetId,
                        async dup =>
                        {
                            var packet = new MqttPublishControlPacket(dup, qos, false, topic, packetId, payload);
                            await _channel.Send(packet, cancellationToken);
                        },
                        _redeliveryIntervalMs,
                        cancellationToken);
                    break;
                case MqttMessageQos.QOS_2:
                    await RetryUntilSpecificMessageReceived<MqttPubrecControlPacket>(
                        x => x.PacketIdentifier == packetId,
                        async dup =>
                        {
                            var packet = new MqttPublishControlPacket(dup, qos, false, topic, packetId, payload);
                            await _channel.Send(packet, cancellationToken);
                        },
                        _redeliveryIntervalMs,
                        cancellationToken);
                    var pubcompWaiter = WaitSpecificMessage<MqttPubcompControlPacket>(
                        x => x.PacketIdentifier == packetId,
                        _discardTimeoutMs,
                        cancellationToken);
                    await _channel.Send(new MqttPubrelControlPacket(packetId), cancellationToken);
                    await pubcompWaiter;
                    break;
            }
            
            Console.WriteLine("Message sent");
        }

        public async Task Acknowledge(MqttMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            switch (message.Qos)
            {
                case MqttMessageQos.QOS_0:
                    return;
                case MqttMessageQos.QOS_1:
                    await _channel.Send(new MqttPubackControlPacket(message.PacketIdentifier), cancellationToken);
                    return;
                case MqttMessageQos.QOS_2:
                    var pubrelWaiter = WaitSpecificMessage<MqttPubrelControlPacket>(
                        x => x.PacketIdentifier == message.PacketIdentifier, _discardTimeoutMs, cancellationToken);
                    
                    await _channel.Send(new MqttPubrecControlPacket(message.PacketIdentifier), cancellationToken);
                    
                    if (await pubrelWaiter != null)
                        await _channel.Send(new MqttPubcompControlPacket(message.PacketIdentifier), cancellationToken);
                    
                    return;
            }
        }

        public async Task Subscribe(string topic, MqttMessageQos qos, CancellationToken cancellationToken)
        {
            await _channel.Send(new MqttSubscribeControlPacket(AllocatePacketId(),
                new[] {new TopicFilter(topic, qos)}), cancellationToken);
        }
        
        private MqttMessage HandleMessage(MqttPublishControlPacket packet)
        {
            var message = new MqttMessage(packet);
            
            switch (packet.Qos)
            {
                case MqttMessageQos.QOS_1:
                case MqttMessageQos.QOS_2:
                    if (_autoAck)
                        Acknowledge(message, _globalCts.Token);
                    return message;
                default:
                    return message;
            }
        }

        public void Dispose()
        {
            _globalCts.Cancel();
            _globalCts.Dispose();
            _channel.Dispose();
        }
    }
}