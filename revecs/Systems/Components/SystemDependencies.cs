using revecs.Core;
using revecs.Extensions.Buffers;
using revecs.Utility;
using Setup = revecs.Extensions.Buffers.BufferComponentSetup<revecs.Systems.SystemDependencies>;

namespace revecs.Systems;

public struct SystemDependencies
{
    /// <summary>
    /// The system dependency
    /// </summary>
    public UEntityHandle Other;
    /// <summary>
    /// Whether or not it need to wait for the dependency to be successfully completed
    /// </summary>
    /// <remarks>
    /// If false and the dependency was either not queued or not sucessfully completed, it will skip it.
    /// </remarks>
    public bool RequireSuccess;

    public static ComponentType<BufferData<SystemDependencies>> GetComponentType(RevolutionWorld world)
    {
        var name = ManagedTypeData<SystemDependencies>.Name;

        var componentType = world.GetComponentType<BufferData<SystemDependencies>>(name);
        if (componentType.Equals(default))
            componentType = world.RegisterComponent<Setup, BufferData<SystemDependencies>>();

        return componentType;
    }
}