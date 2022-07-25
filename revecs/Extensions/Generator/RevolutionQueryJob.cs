using revecs.Querying;
using revecs.Systems;
using revtask.Core;

namespace revecs.Extensions.Generator;

public struct RevolutionQueryJob<TQuery, TData> : IJob
    where TQuery : IRevolutionQuery
{
    public readonly bool ForceSingleThread;
    
    private TQuery _query;
    private object _action;
    
    public RevolutionQueryJob(TQuery query, object action, bool forceSingleThread = false)
    {
        ForceSingleThread = forceSingleThread;
        
        _action = action;
        _query = query;
    }

    private SystemState<TData> _state;
    private int _entityCount;
    
    private int GetEntityToProcess(int taskCount)
    {
        return Math.Max((int)Math.Ceiling((float) _entityCount / taskCount), 1);
    }

    public void PrepareData(TData data)
    {
        _state.Data = data;
    }

    public int SetupJob(JobSetupInfo info)
    {
        _state.World = _query.Query.World;
        _entityCount = _query.Query.GetEntityCount();

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
        while (_query.Query.EntitySliceAt(ref start, ref count, out var span))
        {
            // _action(span, _state);
            _query.ParallelOnEntities(span, _state, _action);
        }
    }
}