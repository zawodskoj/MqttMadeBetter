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
    
    public class MqttProtocolViolationException : MqttClientException
    {
        public MqttProtocolViolationException(string message) : base(message) { }

        public MqttProtocolViolationException(string message, Exception innerException) : base(message, innerException) { }
    }

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
        private readonly ushort _keepAliveSeconds;
        private readonly bool _autoAck;
        private volatile int _packetIdCntr;
        private readonly CancellationTokenSource _globalCts = new CancellationTokenSource();

        private readonly IObservable<MqttControlPacket> _packets;
        private event Action<MqttControlPacket> PacketsEv;

        private MqttClient(MqttChannel channel, ushort keepAliveSeconds, bool autoAck)
        {
            _channel = channel;
            _keepAliveSeconds = keepAliveSeconds;
            _autoAck = autoAck;
            
            _packets = Observable.FromEvent<MqttControlPacket>(x => PacketsEv += x, x => PacketsEv -= x,
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
            try
            {
                await _channel.Send(packet, cancellationToken);
                var connack = await _channel.Receive(cancellationToken);
                if (!(connack is MqttConnackControlPacket realConnack))
                    throw Error("Unexpected packet received, expected CONNACK, got " + connack.Type);

                _ = StartReceiving();

                if (realConnack.ReturnCode != MqttConnackReturnCode.ACCEPTED)
                    throw Error(TranslateConnackReturnCode(realConnack.ReturnCode));

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
            const int defaultPingInterval = 5000;
            
            var pingInterval = _keepAliveSeconds == 0 ? defaultPingInterval : _keepAliveSeconds * 500;

            var stopwatch = new Stopwatch();

            var token = _globalCts.Token;
            while (!_globalCts.IsCancellationRequested)
            {
                var waiter = _packets.WaitSpecificMessage<MqttPingrespControlPacket>(_ => true, pingInterval, token);
                await Send(new MqttPingreqControlPacket(), token);

                stopwatch.Restart();
                
                if (await Task.WhenAny(waiter, Task.Delay(pingInterval, _globalCts.Token)) != waiter)
                {
                    // No ping received
                    // TODO: notify about error

                    Dispose();
                    return;
                }

                var elapsed = stopwatch.ElapsedMilliseconds;
                await Task.Delay((int) Math.Max(0, pingInterval - elapsed), _globalCts.Token);
            }
        }
        
        public IObservable<MqttMessage> Messages { get; }

        private MqttClientException Error(string s, Exception e = null)
        {
            Dispose();
            return e != null ? new MqttClientException(s, e) : new MqttClientException(s);
        }
        
        private MqttProtocolViolationException Violation(string s, Exception e = null)
        {
            Dispose();
            return e != null ? new MqttProtocolViolationException(s, e) : new MqttProtocolViolationException(s);
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

        private async Task<T> SendAndReceive<T>(MqttControlPacketWithId packet, int receiveTimeoutMs, CancellationToken token) where T : MqttControlPacketWithId
        {
            var waiter = _packets.WaitSpecificMessage<T>(x => x.PacketIdentifier == packet.PacketIdentifier, receiveTimeoutMs, token);

            await Send(packet, token);
            return await waiter ?? throw Violation($"Expected {typeof(T).Name}, did not receive in {receiveTimeoutMs} ms, closing connection");
        }

        private ushort AllocatePacketId()
        {
            return (ushort) (1 + Interlocked.Increment(ref _packetIdCntr) % 0xffff);
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
                    await SendAndReceive<MqttPubackControlPacket>(packet, 500, cancellationToken);
                    break;
                case MqttMessageQos.QOS_2:
                    await SendAndReceive<MqttPubrecControlPacket>(packet, 500, cancellationToken);
                    var pubrel = new MqttPubrelControlPacket(packetId);
                    await SendAndReceive<MqttPubcompControlPacket>(pubrel, 500, cancellationToken);
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
                    await Send(new MqttPubackControlPacket(message.PacketIdentifier), cancellationToken);
                    return;
                case MqttMessageQos.QOS_2:
                    await SendAndReceive<MqttPubrelControlPacket>(
                        new MqttPubrecControlPacket(message.PacketIdentifier), 500, cancellationToken);
                    await Send(new MqttPubcompControlPacket(message.PacketIdentifier), cancellationToken);
                    
                    return;
            }
        }

        public async Task Subscribe(string topic, MqttMessageQos qos, CancellationToken cancellationToken)
        {
            await Send(new MqttSubscribeControlPacket(AllocatePacketId(),
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
                        _ = Acknowledge(message, _globalCts.Token);
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