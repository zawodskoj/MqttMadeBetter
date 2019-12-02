using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zw.MqttMadeBetter.Client.Auto
{
    internal class AsyncQueue<T> : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private TaskCompletionSource<T> _itemAvailable;
        
        public void Enqueue(T item)
        {
            lock (_lock)
            {
                _queue.Enqueue(item);

                if (_itemAvailable != null)
                {
                    _itemAvailable.TrySetResult(item);
                    _itemAvailable = null;
                }
            }
        }

        public async Task WaitAndProcess(Func<T, Task> process, CancellationToken cancellationToken)
        {
            using (await _semaphore.Enter(cancellationToken)) 
            {
                TaskCompletionSource<T> tcs = null;

                T item;
                
                lock (_lock)
                {
                    if (!_queue.TryPeek(out item))
                    {
                        tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                        cancellationToken.Register(() => tcs.TrySetCanceled());
                        _itemAvailable = tcs;
                    }
                }

                if (tcs != null)
                    item = await _itemAvailable.Task.ConfigureAwait(false);

                await process(item);

                lock (_lock)
                {
                    _queue.Dequeue();
                }
            }
        }

        public async Task<bool> TryProcess(Func<T, Task> process, CancellationToken cancellationToken)
        {
            using (await _semaphore.Enter(cancellationToken)) 
            {
                T item;
                
                lock (_lock)
                {
                    if (!_queue.TryPeek(out item)) return false;
                }

                await process(item);

                lock (_lock)
                {
                    _queue.Dequeue();
                }

                return true;
            }
        }

        public async Task<bool> TryProcess(Action<T> process, CancellationToken cancellationToken)
        {
            using (await _semaphore.Enter(cancellationToken)) 
            {
                T item;
                
                lock (_lock)
                {
                    if (!_queue.TryPeek(out item)) return false;
                }

                process(item);

                lock (_lock)
                {
                    _queue.Dequeue();
                }

                return true;
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}