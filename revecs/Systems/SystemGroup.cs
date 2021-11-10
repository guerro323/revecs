using revecs.Core;
using revecs.Extensions.Buffers;
using revtask.Core;
using revtask.Helpers;

namespace revecs.Systems;

public class SystemGroup
{
    record struct RSystem(UEntityHandle Handle, ISystem Execution);

    private readonly List<ISystem> _queuedCreateSystems = new();
    private readonly List<RSystem> _systems = new();

    public readonly RevolutionWorld World;

    private readonly ComponentType<JobRequest> _currentSystemBatchComponent;
    private readonly ComponentType<BufferData<SystemDependencies>> _systemDependenciesComponent;
    private readonly ComponentType<SystemState> _systemStateComponent;

    public SystemGroup(RevolutionWorld world)
    {
        World = world;

        _currentSystemBatchComponent = CurrentSystemJobRequest.GetComponentType(world);
        _systemDependenciesComponent = SystemDependencies.GetComponentType(world);
        _systemStateComponent = SystemStateStatic.GetComponentType(world);
    }

    public void Add<T>(T system)
        where T : ISystem
    {
        // TODO: introduce GenericCollection
        _queuedCreateSystems.Add(system);
    }

    private readonly List<JobRequest> _batches = new();

    public JobRequest Schedule(IJobRunner runner)
    {
        // Phase 0 - Create Systems and add them to the update loop
        while (_queuedCreateSystems.Count > 0)
        {
            var system = _queuedCreateSystems[^1];
            var handle = World.CreateEntity();

            if (system.Create(handle, World))
            {
                World.AddComponent(handle, _currentSystemBatchComponent, default);
                World.AddComponent(handle, _systemDependenciesComponent, default);
                World.AddComponent(handle, _systemStateComponent, default);

                _systems.Add(new RSystem(handle, system));
            }
            else
            {
                // revert prev version
                World.DestroyEntity(handle);
            }

            _queuedCreateSystems.RemoveAt(_queuedCreateSystems.Count - 1);
        }

        _batches.Clear();

        // Phase 1 - Clear previous data from runs
        foreach (var (handle, _) in _systems)
        {
            // Clear previous dependencies
            World.GetComponentData(handle, _currentSystemBatchComponent) = default;
            World.GetComponentData(handle, _systemDependenciesComponent).Clear();
            World.GetComponentData(handle, _systemStateComponent) = SystemState.None;
        }

        // Phase 2 - Prepare systems (PreQueue)
        foreach (var (handle, system) in _systems)
        {
            system.PreQueue(handle, World);
        }

        // Phase 3 - Queue batches
        foreach (var (handle, system) in _systems)
        {
            {
                World.GetComponentData(handle, _systemStateComponent) = SystemState.Queued;
            }
            var systemBatch = system.Queue(handle, World, runner);
            {
                World.GetComponentData(handle, _currentSystemBatchComponent) = systemBatch;
                if (systemBatch == default)
                    World.GetComponentData(handle, _systemStateComponent) = SystemState.None;
            }

            if (systemBatch != default)
                _batches.Add(systemBatch);
        }

        if (_batches.Count == 0)
            return default;

        return runner.WaitBatches(_batches);
    }
}