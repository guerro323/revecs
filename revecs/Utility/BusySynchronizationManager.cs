using System.Runtime.CompilerServices;

namespace revecs.Utility
{
    /// <summary>
    /// A synchronization manager that is made for very short locking time (less than 10ns)
    /// </summary>
    public class BusySynchronizationManager
    {
        private int _owner;
        private int _depth;

        public BusySynchronizationManager()
        {
            _owner = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SyncContext Synchronize()
        {
            return new SyncContext(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            var threadId = Environment.CurrentManagedThreadId;

            var iter = 0;
            while (Interlocked.CompareExchange(ref _owner, threadId, 0) != threadId)
            {
                iter++;
            }

            /*if (iter > 10_000)
            {
                Console.WriteLine(iter + " " + Environment.StackTrace);
            }*/

            if (iter == 0)
                _depth++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unlock()
        {
            if (_depth > 0)
            {
                _depth--;
                return;
            }
            
            var threadId = Environment.CurrentManagedThreadId;
            if (threadId != Interlocked.Exchange(ref _owner, 0))
                throw new UnauthorizedAccessException("Unlocking failure");
            
            Interlocked.MemoryBarrier();
        }

        public readonly struct SyncContext : IDisposable
        {
            private readonly BusySynchronizationManager _synchronizer;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public SyncContext(BusySynchronizationManager synchronizer)
            {
                _synchronizer = synchronizer;
                _synchronizer.Lock();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _synchronizer.Unlock();
            }
        }
    }
}