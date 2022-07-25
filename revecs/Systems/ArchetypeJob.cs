using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Querying;
using revtask.Core;

namespace revecs.Systems;

public struct SystemState<T>
{
    public RevolutionWorld World;
    public T Data;

    public void Deconstruct(out RevolutionWorld world, out T data)
    {
        world = World;
        data = Data;
    }
}

public struct ArchetypeJob<T> : IJob
{
    public readonly bool ForceSingleThread;

    public delegate void OnArchetype(in ReadOnlySpan<UEntityHandle> entities, in SystemState<T> state);

    private OnArchetype _action;
    private ArchetypeQuery _query;
    
    public ArchetypeJob(OnArchetype action, ArchetypeQuery query, bool forceSingleThread = false)
    {
        ForceSingleThread = forceSingleThread;
        
        _action = action;
        _query = query;
    }

    private SystemState<T> _state;
    private int _entityCount;
    
    private int GetEntityToProcess(int taskCount)
    {
        return Math.Max((int)Math.Ceiling((float) _entityCount / taskCount), 1);
    }

    public void PrepareData(T data)
    {
        _state.Data = data;
    }

    public int SetupJob(JobSetupInfo info)
    {
        _state.World = _query.World;
        _entityCount = _query.GetEntityCount();

        return _entityCount == 0
            ? 0
            : ForceSingleThread
                ? 1
                : Math.Max((int)Math.Ceiling((float)_entityCount / GetEntityToProcess(info.TaskCount)), 1);
    }

    public void Execute(IJobRunner runner, JobExecuteInfo info)
    {
        int start, end;
        if (ForceSingleThread)
        {
            start = 0;
            end = _entityCount;
        }
        else
        {
            var entityToProcess = GetEntityToProcess(info.TaskCount);

            var batchSize = info.Index == info.MaxUseIndex
                ? _entityCount - (entityToProcess * info.Index)
                : entityToProcess;

            start = entityToProcess * info.Index;
            end = start + batchSize;

            if (start >= _entityCount)
                return;
        }

        var count = Math.Min(_entityCount, end) - start;
        while (_query.EntitySliceAt(ref start, ref count, out var span))
        {
            _action(span, _state);
        }
    }

    public void Update(UEntityHandle handle, in T state)
    {
        _action(MemoryMarshal.CreateSpan(ref handle, 1), new SystemState<T>
        {
            Data = state,
            World = _query.World
        });
    }

    public void Update(in T state)
    {
        var start = 0;
        var count = _query.GetEntityCount();
        while (_query.EntitySliceAt(ref start, ref count, out var span))
        {
            _action(span, new SystemState<T>
            {
                Data = state,
                World = _query.World
            });
        }
    }
}

public static class ArchetypeJobExtension
{
    public static ArchetypeJob<T> Job<T>(this ArchetypeQuery query, ArchetypeJob<T>.OnArchetype action, T data,
        bool singleThreaded = false)
    {
        var job = new ArchetypeJob<T>(action, query, singleThreaded);
        job.PrepareData(data);

        return job;
    }

    public static JobRequest Queue<T>(this ArchetypeQuery query, IJobRunner runner, ArchetypeJob<T>.OnArchetype action,
        T data, bool singleThreaded = false)
    {
        return runner.Queue(query.Job(action, data, singleThreaded));
    }

    public static void QueueAndComplete<T>(this ArchetypeQuery query, IJobRunner runner,
        ArchetypeJob<T>.OnArchetype action,
        T data, bool singleThreaded = false)
    {
        runner.QueueAndComplete(query.Job(action, data, singleThreaded));
    }

    public static JobRequest Queue(this ArchetypeQuery query, IJobRunner runner,
        ArchetypeJob<ValueTuple>.OnArchetype action,
        bool singleThreaded = false)
    {
        return runner.Queue(query.Job(action, default, singleThreaded));
    }

    public static void QueueAndComplete(this ArchetypeQuery query, IJobRunner runner,
        ArchetypeJob<ValueTuple>.OnArchetype action,
        bool singleThreaded = false)
    {
        runner.QueueAndComplete(query.Job(action, default, singleThreaded));
    }
}