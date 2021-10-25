namespace revecs.Core.Boards
{
    // Low performance, and fix race bugs
    
    /*public delegate void PreSwitchEvent(Span<UEntityHandle> entities);

    public sealed class ArchetypeUpdateBoard : BoardBase
    {
        private readonly BatchRunnerBoard _runnerBoard;
        private readonly ArchetypeBoard _archetypeBoard;
        private readonly EntityComponentLinkBoard _componentLinkBoard;
        private readonly ComponentTypeBoard _componentTypeBoard;
        private readonly EntityBoard _entityBoard;

        private readonly BindableListener _entityOnResizeListener;
        private (int[] queueIndex, UEntityHandle[] update) _column;

        private SwapDependency[] _dependencyPerEntity;
        private List<JobRequest> _requests = new();
        private SynchronizationManager _requestSync = new();

        private int _updateCount;

        private List<PreSwitchEvent> _preSwitchList = new();
        public event PreSwitchEvent PreSwitch
        {
            add => _preSwitchList.Add(value);
            remove => _preSwitchList.Remove(value);
        }
        
        public ArchetypeUpdateBoard(RevolutionWorld world) : base(world)
        {
            _column.update = Array.Empty<UEntityHandle>();
            _column.queueIndex = Array.Empty<int>();

            _runnerBoard = world.GetBoard<BatchRunnerBoard>("Runner");
            _entityBoard = world.GetBoard<EntityBoard>("Entity");
            _archetypeBoard = world.GetBoard<ArchetypeBoard>("Archetype");
            _componentTypeBoard = world.GetBoard<ComponentTypeBoard>("ComponentType");
            _componentLinkBoard = world.GetBoard<EntityComponentLinkBoard>("EntityComponentLink");

            _entityOnResizeListener = _entityBoard.CurrentSize.Subscribe(EntityOnResize, true);
        }
        
#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void updateArchetype(in UEntityHandle handle)
        {
            GameWorldLowLevel.UpdateArchetype(
                _archetypeBoard,
                _componentTypeBoard,
                _entityBoard,
                _componentLinkBoard,
                handle
            );

            ref var index = ref _column.queueIndex[handle.Id];
            _column.update[index] = default;

            index = -1;

            _updateCount--;
        }

        [ThreadStatic] private static List<JobRequest>? _copy;

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Update()
        {
            _copy ??= new List<JobRequest>();
            
            _requestSync.Lock();
            _copy.Clear();
            _copy.AddRange(_requests);
            _requests.Clear();
            _requestSync.Unlock();
            
            foreach (var request in _copy)
            {
                _runnerBoard.Runner.CompleteBatch(request);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(UEntityHandle handle)
        {
            _runnerBoard.Runner.CompleteBatch(_dependencyPerEntity[handle.Id].GetWriterHandle());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Queue(UEntityHandle handle)
        {
            using var sync = _requestSync.Synchronize();
            _requests.Add(_runnerBoard.Runner.Queue(new UpdateJob
            {
                Owner = this,
                Entity = handle,
                Dependency = _dependencyPerEntity[handle.Id]
            }));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dequeue(UEntityHandle handle)
        {
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
            Array.Resize(ref _dependencyPerEntity, current);

            for (; previous < current; previous++)
            {
                _column.queueIndex[previous] = -1;
                _dependencyPerEntity[previous] = new SwapDependency();
            }
        }

        public override void Dispose()
        {
            _entityOnResizeListener.Dispose();
        }
        
        private struct UpdateJob : IJob, IJobExecuteOnCondition, IJobSetHandle
        {
            public ArchetypeUpdateBoard Owner;
            
            public UEntityHandle Entity;
            public SwapDependency Dependency;
            
            public int SetupJob(JobSetupInfo info) => 1;

            public void Execute(IJobRunner runner, JobExecuteInfo info)
            {
                GameWorldLowLevel.UpdateArchetype(
                    Owner._archetypeBoard,
                    Owner._componentTypeBoard,
                    Owner._entityBoard,
                    Owner._componentLinkBoard,
                    Entity
                );

                Owner._requestSync.Lock();
                Owner._requests.Remove(info.Request);
                Owner._requestSync.Unlock();
            }

            public bool CanExecute(IJobRunner runner, JobExecuteInfo info)
            {
                return Dependency.TrySwap(runner, info.Request);
            }

            public void SetHandle(IJobRunner runner, JobRequest handle)
            {
                var span = Entity.ToSpan();
                foreach (ref readonly var ev in CollectionsMarshal.AsSpan(Owner._preSwitchList))
                    ev(span);
                
                // Try an optimist trySwap
                Dependency.TrySwap(runner, handle);
            }
        }
    }*/
}