using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zw.MqttMadeBetter.Channel.ControlPackets;

namespace Zw.MqttMadeBetter.Client.Auto
{
    // ReSharper disable once EnumUnderlyingTypeIsInt
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
        private class TopicSubscription : IEquatable<TopicSubscription>
        {
            public TopicSubscription(string topic, MqttMessageQos qos)
            {
                Topic = topic;
                Qos = qos;
            }

            public string Topic { get; }
            public MqttMessageQos Qos { get; }

            public bool Equals(TopicSubscription other) =>
                other != null && Topic == other.Topic && Qos == other.Qos;

            public override bool Equals(object obj) => 
                obj is TopicSubscription other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Topic != null ? Topic.GetHashCode() : 0) * 397) ^ (int) Qos;
                }
            }
        }

        private readonly struct TopicAction
        {
            public TopicAction(string topic, MqttMessageQos qos, bool subscribe)
            {
                Topic = topic;
                Qos = qos;
                Subscribe = subscribe;
            }

            public string Topic { get;}
            public MqttMessageQos Qos { get; }
            public bool Subscribe { get; }
        }
        
        private readonly struct MessageToSend
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

        private event Action<MqttMessage> MessagesEv;
        private event Action<MqttConnectionStateChange> StateChangesEv;

        private readonly object _lock = new object();
        private volatile MqttConnectionState _state;
        private volatile CancellationTokenSource _curCts;
        private bool _disposed;

        private readonly SemaphoreSlim _subscriptionsSemaphore = new SemaphoreSlim(1);
        private readonly HashSet<TopicSubscription> _subscriptions =
            new HashSet<TopicSubscription>();
        
        private readonly AsyncQueue<TopicAction> _topicActions = new AsyncQueue<TopicAction>();
        
        public MqttAutoClient(MqttReadOnlyAutoClientOptions options)
        {
            _options = options;
            
            Messages = Observable.FromEvent<MqttMessage>(x => MessagesEv += x, x => MessagesEv -= x);
            StateChanges = Observable.FromEvent<MqttConnectionStateChange>(x => StateChangesEv += x, x => StateChangesEv -= x);
        }

        private async Task Run(CancellationToken cancellationToken)
        {
            var numOfFailedRetries = 0;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await MqttClient.Create(_options.ClientOptions, cancellationToken);
                    
                    lock (_lock)
                    {
                        switch (_state)
                        {
                            case MqttConnectionState.STARTING:
                            case MqttConnectionState.RESTARTING:
                                SetState(MqttConnectionState.STARTED);
                                break;
                            case MqttConnectionState.STARTED:
                                // wtf?
                                break;
                            case MqttConnectionState.STOPPED:
                                _ = client.Disconnect(false, CancellationToken.None);
                                return;
                        }
                    }
                    
                    using var __ = client.Messages.Subscribe(x => MessagesEv?.Invoke(x));
                    var retryTcs = new TaskCompletionSource<(bool Canceled, MqttClientException Exception)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                    client.ConnectionClosed += (o, e) => retryTcs.TrySetResult((false, e));
                    
                    await Resubscribe(client);
                    _ = SendLoop(client);
                    _ = SubLoop(client);

                    await using (cancellationToken.Register(() => retryTcs.TrySetResult((true, null))))
                    {
                        numOfFailedRetries = 0;
                        
                        var (canceled, exception) = await retryTcs.Task;
                        if (canceled)
                        {
                            await client.Disconnect(false, CancellationToken.None);
                        }
                        
                        lock (_lock)
                        {
                            switch (_state)
                            {
                                case MqttConnectionState.STOPPED:
                                    return;
                                default:
                                    SetState(MqttConnectionState.RESTARTING, exception);
                                    break;
                            }
                        }

                        if (cancellationToken.IsCancellationRequested)
                            return;
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
                                SetState(MqttConnectionState.RESTARTING, Error("Connection failed", e));
                                break;
                        }
                    }
                        
                    await Task.Delay(_options.Backoff(numOfFailedRetries++), cancellationToken);
                }
            }
            
            MqttClientException Error(string text, Exception inner = null) => new MqttClientException(text, inner);

            void SetState(MqttConnectionState state, MqttClientException e = null)
            {
                _state = state;
                StateChangesEv?.Invoke(new MqttConnectionStateChange(state, e));
            }

            async Task Resubscribe(MqttClient client)
            {
                // todo: bulk sub
                using (await _subscriptionsSemaphore.Enter(cancellationToken))
                {
                    foreach (var sub in _subscriptions)
                    {
                        await client.Subscribe(sub.Topic, sub.Qos, cancellationToken);
                    }
                }
            }
            
            async Task SendLoop(MqttClient client)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await _publishPackets.WaitAndProcess(
                        m => client.Send(m.Topic, m.Qos, m.Payload, cancellationToken), cancellationToken);
                }
            }
                    
            async Task SubLoop(MqttClient client)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await _topicActions.WaitAndProcess(async m =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using (await _subscriptionsSemaphore.Enter(cancellationToken))
                        {
                            if (m.Subscribe)
                            {
                                if (_subscriptions.All(x => x.Topic != m.Topic))
                                {
                                    await client.Subscribe(m.Topic, m.Qos, cancellationToken);
                                    _subscriptions.Add(new TopicSubscription(m.Topic, m.Qos));
                                }
                            }
                            else
                            {
                                await client.Unsubscribe(m.Topic, cancellationToken);
                                _subscriptions.Remove(new TopicSubscription(m.Topic, m.Qos));
                            }
                        }
                    }, cancellationToken);
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
                        StateChangesEv?.Invoke(new MqttConnectionStateChange(MqttConnectionState.STARTING, null));
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
                        StateChangesEv?.Invoke(new MqttConnectionStateChange(MqttConnectionState.STOPPED, null));
                        return;
                }
            }
        }

        public void Subscribe(string topic, MqttMessageQos qos)
        {
            if (_disposed) 
                throw new ObjectDisposedException("MqttAutoClient");
            
            _topicActions.Enqueue(new TopicAction(topic, qos, true));
        }

        public void Unsubscribe(string topic)
        {
            if (_disposed)
                throw new ObjectDisposedException("MqttAutoClient");
            
            _topicActions.Enqueue(new TopicAction(topic, default, false));
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