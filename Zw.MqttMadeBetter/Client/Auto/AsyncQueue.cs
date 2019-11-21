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
                    item = await _itemAvailable.Task;

                await process(item);

                lock (_queue)
                {
                    _queue.Dequeue();
                }
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}