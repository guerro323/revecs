using revecs.Core;
using revecs.Utility;
using Setup = revecs.Core.Components.SparseComponentSetup<revecs.Systems.SystemState>;

namespace revecs.Systems;

public enum SystemState
{
    /// <summary>
    /// No State
    /// </summary>
    None,
    /// <summary>
    /// The system batch is waiting for creation
    /// </summary>
    WaitingCreation,
    /// <summary>
    /// The system batch has been created and is currently running
    /// </summary>
    Queued,
    /// <summary>
    /// The system batch has successfully completed
    /// </summary>
    RanToCompletion,
    /// <summary>
    /// The system batch has been internally completed but has been cancelled
    /// </summary>
    RanToCancellation
}

public static class SystemStateStatic
{
    public static ComponentType<SystemState> GetComponentType(RevolutionWorld world)
    {
        var name = ManagedTypeData<SystemState>.Name;

        var componentType = world.GetComponentType<SystemState>(name);
        if (componentType.Equals(default))
            componentType = world.RegisterComponent<Setup, SystemState>();

        return componentType;
    }
}