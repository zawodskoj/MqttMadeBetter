using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zw.MqttMadeBetter
{
    public static class SemaphoreExtensions
    {
        public class SemaphoreDisposable : IDisposable
        {
            private bool _disposed;
            public SemaphoreSlim Semaphore { get; }

            public SemaphoreDisposable(SemaphoreSlim semaphore)
            {
                Semaphore = semaphore;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    Semaphore.Release();
                }
            }
        }

        public static async Task<SemaphoreDisposable> Enter(this SemaphoreSlim semaphore, CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            return new SemaphoreDisposable(semaphore);
        } 
    }
}