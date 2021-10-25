using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Utility;

namespace revecs.Core.Boards
{
    public delegate void PreSwitchEvent(Span<UEntityHandle> entities);

    public sealed class ArchetypeUpdateBoard : BoardBase
    {
        private readonly BatchRunnerBoard _runnerBoard;
        private readonly ArchetypeBoard _archetypeBoard;
        private readonly EntityComponentLinkBoard _componentLinkBoard;
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

        private BusySynchronizationManager _synchronization = new();
        
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

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Update()
        {
            using var sync = _synchronization.Synchronize();
            
            var span = _column.update.AsSpan(0, _updateCount);
            foreach (ref readonly var ev in CollectionsMarshal.AsSpan(_preSwitchList))
                ev(span);

            var count = _updateCount;
            for (var i = 0; i < count; i++)
            {
                var entity = _column.update[i];
                if (entity.Id != 0) updateArchetype(entity);
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
            using var sync = _synchronization.Synchronize();
            
            ref var index = ref _column.queueIndex[handle.Id];
            if (index >= 0)
                return; // already queued
            
            index = _updateCount++;
            _column.update[index] = handle;
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