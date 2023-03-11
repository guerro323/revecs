using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Utility;
using revghost.Shared.Events;
using revghost.Shared.Threading;
using revtask.Core;

namespace revecs.Core.Boards
{
    public delegate void PreSwitchEvent(Span<UEntityHandle> entities);

    public sealed class ArchetypeUpdateBoard : BoardBase
    {
        private readonly ArchetypeBoard _archetypeBoard;
        private readonly EntityHasComponentBoard _hasComponentBoard;
        private readonly ComponentTypeBoard _componentTypeBoard;
        private readonly EntityBoard _entityBoard;

        private readonly BindableListener _entityOnResizeListener;
        private (int[] queueIndex, UEntityHandle[] update) _column;
        
        private int _updateCount;

        private List<PreSwitchEvent> _preSwitchList = new();
        public event PreSwitchEvent PreSwitch
        {
            add => _preSwitchList.Add(value);
            remove => _preSwitchList.Remove(value);
        }

        private TestSynchro _synchronization = new();
        
        public ArchetypeUpdateBoard(RevolutionWorld world) : base(world)
        {
            _column.update = Array.Empty<UEntityHandle>();
            _column.queueIndex = Array.Empty<int>();

            _entityBoard = world.EntityBoard;
            _archetypeBoard = world.ArchetypeBoard;
            _componentTypeBoard = world.ComponentTypeBoard;
            _hasComponentBoard = world.EntityHasComponentBoard;

            _entityOnResizeListener = _entityBoard.CurrentSize.Subscribe(EntityOnResize, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void removeEntity(UEntityHandle handle)
        {
            ref var index = ref _column.queueIndex[handle.Id];
            _column.update[index] = default;

            index = -1;

            _updateCount--;
        }
        
#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void updateArchetype(UEntityHandle handle)
        {
            GameWorldLowLevel.UpdateArchetype(
                _archetypeBoard,
                _componentTypeBoard,
                _entityBoard,
                _hasComponentBoard,
                handle
            );
            
            removeEntity(handle);
        }

        record struct UpdateArchetypesJob(ArchetypeUpdateBoard Self) : IJob
        {
            private int _entityCount;
            
            private static int GetEntityToProcess(int entityCount, int taskCount)
            {
                return Math.Max((int)Math.Ceiling((float) entityCount / taskCount), 1);
            }
            
            public int SetupJob(JobSetupInfo info)
            {
                var count = _entityCount = Self._updateCount;
                return Math.Max((int) Math.Ceiling((float) count / GetEntityToProcess(count, info.TaskCount)), 1);
            }

            public void Execute(IJobRunner runner, JobExecuteInfo info)
            {
                var entityToProcess = GetEntityToProcess(_entityCount, info.TaskCount);

                var batchSize = info.Index == info.MaxUseIndex
                    ? _entityCount - (entityToProcess * info.Index)
                    : entityToProcess;

                var start = entityToProcess * info.Index;
                var end = Math.Min(_entityCount, start + batchSize);

                for (; start < end; start++)
                {
                    var handle = Self._column.update[start];
                    if (handle.Id == default)
                        continue;

                    Self.updateArchetype(handle);
                }
            }
        }

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Update()
        {
            if (_updateCount == 0)
                return;
            
            using var sync = _synchronization.Synchronize();
            var span = _column.update.AsSpan(0, _updateCount);
            foreach (ref readonly var ev in CollectionsMarshal.AsSpan(_preSwitchList))
                ev(span);
            
            var runnerBoard = World.GetBoardOrDefault<BatchRunnerBoard>(nameof(BatchRunnerBoard));
            if (runnerBoard != null)
            {
                runnerBoard.Runner.QueueAndComplete(new UpdateArchetypesJob(this));
            }
            else
            {
                var count = _updateCount;
                for (var i = 0; i < count; i++)
                {
                    var entity = _column.update[i];
                    if (entity.Id != 0) updateArchetype(entity);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(UEntityHandle handle)
        {
            using var sync = _synchronization.Synchronize();
            
            if (_column.queueIndex[handle.Id] < 0)
                return; // not queued

            var span = handle.ToSpan();
            foreach (ref readonly var ev in CollectionsMarshal.AsSpan(_preSwitchList))
                ev(span);
            
            updateArchetype(handle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Queue(UEntityHandle handle)
        {
            //using var sync = _synchronization.Synchronize();
            _synchronization.Lock();
            
            ref var index = ref _column.queueIndex[handle.Id];
            if (index >= 0)
            {
                _synchronization.Unlock();
                return; // already queued
            }

            index = _updateCount++;
            _column.update[index] = handle;
            
            _synchronization.Unlock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dequeue(UEntityHandle handle)
        {
            using var sync = _synchronization.Synchronize();
            
            ref var index = ref _column.queueIndex[handle.Id];
            if (index == 0)
                return;

            _column.update[index] = default;
            index = 0;
        }

        private void EntityOnResize(int previous, int current)
        {
            Array.Resize(ref _column.queueIndex, current);
            Array.Resize(ref _column.update, current);

            for (; previous < current; previous++)
            {
                _column.queueIndex[previous] = -1;
            }
        }

        public override void Dispose()
        {
            _entityOnResizeListener.Dispose();
        }
    }
}

public class TestSynchro
{
    public int _owner;
    public int _depth;

    public TestSynchro() => this._owner = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SyncContext Synchronize() => new SyncContext(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Lock()
    {
        int currentManagedThreadId = Environment.CurrentManagedThreadId;
        int num = 0;
        while (Interlocked.CompareExchange(ref this._owner, currentManagedThreadId, 0) != currentManagedThreadId)
            ++num;
        if (num != 0)
            return;
        ++this._depth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unlock(bool use = false)
    {
        if (this._depth > 0)
        {
            --this._depth;
        }
        else
        {
            if (Environment.CurrentManagedThreadId != Interlocked.Exchange(ref this._owner, 0))
                throw new UnauthorizedAccessException("Unlocking failure");
            if (!use)
                Interlocked.MemoryBarrier();
        }
    }

    public readonly struct SyncContext : IDisposable
    {
        private readonly TestSynchro _synchronizer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SyncContext(TestSynchro synchronizer)
        {
            this._synchronizer = synchronizer;
            this._synchronizer.Lock();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => this._synchronizer.Unlock();
    }
}