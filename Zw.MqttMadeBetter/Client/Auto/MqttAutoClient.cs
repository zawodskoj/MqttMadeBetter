using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zw.MqttMadeBetter.Channel.ControlPackets;

namespace Zw.MqttMadeBetter.Client.Auto
{
    public enum MqttConnectionState : int
    {
        STOPPED,
        STARTING,
        STARTED,
        RESTARTING
    }
    
    public class MqttConnectionStateChange
    {
        public MqttConnectionStateChange(MqttConnectionState state, MqttClientException exception)
        {
            State = state;
            Exception = exception;
        }

        public MqttConnectionState State { get; }
        public MqttClientException Exception { get; }
    }
    
    public class MqttAutoClient : IDisposable
    {
        public readonly struct MessageToSend
        {
            public MessageToSend(string topic, MqttMessageQos qos, ReadOnlyMemory<byte> payload)
            {
                Topic = topic;
                Qos = qos;
                Payload = payload;
            }

            public string Topic { get; }
            public MqttMessageQos Qos { get; }
            public ReadOnlyMemory<byte> Payload { get; }
        } 
        
        private readonly MqttReadOnlyAutoClientOptions _options;

        private readonly AsyncQueue<MessageToSend> _publishPackets = new AsyncQueue<MessageToSend>();

        private event Action<MqttMessage> _messagesEv;
        private event Action<MqttConnectionStateChange> _stateChangesEv;

        private readonly object _lock = new object();
        private volatile MqttConnectionState _state;
        private volatile CancellationTokenSource _curCts;
        private bool _disposed;

        public MqttAutoClient(MqttReadOnlyAutoClientOptions options)
        {
            _options = options;
            
            Messages = Observable.FromEvent<MqttMessage>(x => _messagesEv += x, x => _messagesEv -= x);
            StateChanges = Observable.FromEvent<MqttConnectionStateChange>(x => _stateChangesEv += x, x => _stateChangesEv -= x);
        }

        private async Task Run(CancellationToken cancellationToken)
        {
            var i = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await MqttClient.Create(_options.ClientOptions, cancellationToken);
                    await client.Subscribe("mqtt/test/qos2", MqttMessageQos.QOS_2, cancellationToken);

                    lock (_lock)
                    {
                        switch (_state)
                        {
                            case MqttConnectionState.STARTING:
                            case MqttConnectionState.RESTARTING:
                                _state = MqttConnectionState.STARTED;
                                _stateChangesEv?.Invoke(
                                    new MqttConnectionStateChange(MqttConnectionState.STARTED, null));
                                break;
                            case MqttConnectionState.STARTED:
                                // wtf?
                                break;
                            case MqttConnectionState.STOPPED:
                                _ = client.Disconnect(CancellationToken.None);
                                return;
                        }
                    }

                    var messageSubscription = client.Messages.Subscribe(x => _messagesEv?.Invoke(x));
                    var retryTcs = new TaskCompletionSource<(bool Canceled, MqttClientException Exception)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                    client.ConnectionClosed += (o, e) => { retryTcs.TrySetResult((false, e)); };

                    async Task SendLoop()
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            await _publishPackets.WaitAndProcess(
                                m => client.Send(m.Topic, m.Qos, m.Payload, cancellationToken), cancellationToken);
                        }
                    }

                    _ = SendLoop();

                    await using (cancellationToken.Register(() => retryTcs.TrySetResult((true, null))))
                    {
                        var (canceled, exception) = await retryTcs.Task;
                        if (canceled)
                        {
                            try
                            {
                                await client.Disconnect(CancellationToken.None);
                            }
                            catch
                            {
                                // ignore
                            }
                        }

                        messageSubscription.Dispose();
                        
                        lock (_lock)
                        {
                            switch (_state)
                            {
                                case MqttConnectionState.STOPPED:
                                    return;
                                default:
                                    _state = MqttConnectionState.RESTARTING;
                                    _stateChangesEv?.Invoke(
                                        new MqttConnectionStateChange(MqttConnectionState.RESTARTING, exception));
                                    break;
                            }
                        }

                        if (cancellationToken.IsCancellationRequested)
                            return;
                        
                        await Task.Delay(_options.Backoff(i++), cancellationToken);
                    }
                }
                catch (Exception e)
                {   
                    lock (_lock)
                    {
                        switch (_state)
                        {
                            case MqttConnectionState.STOPPED:
                            case var _ when cancellationToken.IsCancellationRequested:
                                return;
                            default:
                                _state = MqttConnectionState.RESTARTING;
                                _stateChangesEv?.Invoke(new MqttConnectionStateChange(MqttConnectionState.RESTARTING, new MqttClientException("Connection failed with unknown exception", e)));
                                break;
                        }
                    }
                    
                    return;
                }
            }
        }

        public void Start()
        {
            if (_disposed) 
                throw new ObjectDisposedException("MqttAutoClient");
            
            lock (_lock)
            {
                switch (_state)
                {
                    case MqttConnectionState.STOPPED:
                        _curCts = new CancellationTokenSource();
                        _state = MqttConnectionState.STARTING;
                        _stateChangesEv?.Invoke(new MqttConnectionStateChange(MqttConnectionState.STARTING, null));
                        _ = Run(_curCts.Token);
                        return;
                    default:
                        // Already started
                        return;
                }
            }
        }

        public void Stop()
        {
            if (_disposed) 
                throw new ObjectDisposedException("MqttAutoClient");
            
            lock (_lock)
            {
                switch (_state)
                {
                    case MqttConnectionState.STOPPED:
                        // Already stopped
                        return;
                    default:
                        _state = MqttConnectionState.STOPPED;
                        _curCts.Cancel();
                        _curCts = null;
                        _stateChangesEv?.Invoke(new MqttConnectionStateChange(MqttConnectionState.STOPPED, null));
                        return;
                }
            }
        }

        public void Subscribe(string topic, MqttMessageQos qos)
        {
            if (_disposed) 
                throw new ObjectDisposedException("MqttAutoClient");
        }

        public void Unsubscribe(string topic)
        {
            if (_disposed)
                throw new ObjectDisposedException("MqttAutoClient");
        }

        public void Send(string topic, MqttMessageQos qos, ReadOnlyMemory<byte> payload)
        {
            if (_disposed) 
                throw new ObjectDisposedException("MqttAutoClient");
            
            _publishPackets.Enqueue(new MessageToSend(topic, qos, payload));
        }
        
        public IObservable<MqttMessage> Messages { get; }
        public IObservable<MqttConnectionStateChange> StateChanges { get; }
        
        public void Dispose()
        {
            _disposed = true;
            Stop();
        }
    }
}