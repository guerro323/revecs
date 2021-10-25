using revecs.Utility;
using revecs.Utility.Threading;

namespace revecs.Systems;

public class SwapDependency
{
    private readonly List<JobRequest> _readers = new();
    private readonly BusySynchronizationManager _sync = new();

    private JobRequest _currentWriter;
    
    public JobRequest GetWriterHandle()
    {
        _sync.Lock();
        var curr = _currentWriter;
        _sync.Unlock();

        return curr;
    }

    public void AddReader(JobRequest handle)
    {
        _sync.Lock();
        _readers.Add(handle);
        _sync.Unlock();
    }

    public bool IsCompleted(IJobRunner runner, JobRequest caller)
    {
        var writer = GetWriterHandle();
        if (writer == caller)
        {
            return true;
        }

        return runner.IsCompleted(writer);
    }

    public bool TrySwap(IJobRunner runner, JobRequest next)
    {
        var didSwitch = false;

        _sync.Lock();
        
        if (_currentWriter == next || runner.IsCompleted(_currentWriter))
        {
            // Complete readers
            // (should this be a batched dependency?)
            foreach (var reader in _readers)
            {
                // A reader is the next request,
                // This can happen with generated code
                if (reader == next)
                    continue;
                
                if (!runner.IsCompleted(reader))
                {
                    _sync.Unlock();
                    return false;
                }
            }

            _currentWriter = next;

            didSwitch = true;

            _readers.Clear();
        }
        
        _sync.Unlock();

        return didSwitch;
    }
}