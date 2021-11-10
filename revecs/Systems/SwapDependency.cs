using System.Runtime.CompilerServices;
using revghost.Shared.Threading;
using revtask.Core;

namespace revecs.Systems;

public class SwapDependency
{
    public struct Context : IDisposable
    {
        internal static BusySynchronizationManager _globalContext = new();
        internal static int In;

        public void Start()
        {
            _globalContext.Lock();
            In += 1;
        }
        
        public void Dispose()
        {
            In -= 1;
            _globalContext.Unlock();
        }
    }

    /// <summary>
    /// Start a swap context for dependencies
    /// </summary>
    /// <remarks>
    /// This method must always be called when you need to swap one or multiple dependencies.
    /// It also need to be disposed.
    /// If called with multiple dependencies, then group all of them in the SAME context.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Context BeginContext()
    {
        var ctx = new Context();
        ctx.Start();
        
        return ctx;
    }

    public static bool InContext()
    {
        return Context.In > 0;
    }

    private readonly List<JobRequest> _readers = new();

    private JobRequest _currentWriter;
    
    public JobRequest GetWriterHandle()
    {
        if (!InContext())
            throw new InvalidOperationException("SwapDependency.BeginContext() must be called");
        
        return _currentWriter;
    }

    public void AddReader(JobRequest handle)
    {
        // Implicit
        // We are just using the context for a way to synchronize the collection
        using (BeginContext())
        {
            if (!_readers.Contains(handle))
                _readers.Add(handle);
        }
    }

    public bool IsCompleted(IJobRunner runner, JobRequest caller)
    {
        if (!InContext())
            throw new InvalidOperationException("SwapDependency.BeginContext() must be called");
        
        var writer = GetWriterHandle();
        if (writer == caller)
        {
            return true;
        }

        return runner.IsCompleted(writer);
    }

    public bool TrySwap(IJobRunner runner, JobRequest next)
    {
        if (!InContext())
            throw new InvalidOperationException("SwapDependency.BeginContext() must be called");
        
        var didSwitch = false;
        
        // Remove 'next' from readers
        _readers.Remove(next);
        
        if (_currentWriter == next || runner.IsCompleted(_currentWriter))
        {
            // Complete readers
            // (should this be a batched dependency?)
            foreach (var reader in _readers)
            {
                // A reader is the next request,
                // This can happen with generated code
                //
                // But it shouldn't really happen since we already remove ourselves before
                if (reader == next)
                    continue;
                
                if (!runner.IsCompleted(reader))
                {
                    return false;
                }
            }

            _currentWriter = next;

            didSwitch = true;

            _readers.Clear();
        }

        return didSwitch;
    }
}