using System.ComponentModel;
using System.Runtime.CompilerServices;
using revecs.Core;
using revecs.Extensions.Buffers;
using revecs.Querying;

namespace revecs.Systems.Generator;

/// <summary>
/// A higher level version of <see cref="ISystem"/>.
/// </summary>
/// <remarks>
/// This must be used in correlation with the generator
/// </remarks>
public interface IRevolutionSystem
{
    /// <summary>
    /// Create the constraints for this system
    /// </summary>
    /// <param name="sys">The system object</param>
    void Constraints(in SystemObject sys);
    /// <summary>
    /// Create the update body for this system
    /// </summary>
    void Body();

    void AddToSystemGroup(SystemGroup group)
    {
        throw new InvalidOperationException($"{GetType()} was not generated!");
    }
}

public struct SystemObject
{
    public RevolutionWorld World;
    public SystemHandle Handle;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public ComponentType<BufferData<SystemDependencies>> DependenciesType; 
}

public static class RevolutionSystemExtensions
{
    public static void Add<T>(this SystemGroup group, T sys = default!)
        where T : IRevolutionSystem
    {
        sys.AddToSystemGroup(group);
    }
}

public static class SystemObjectExtensions
{
    public static void DependOn<T>(this SystemObject obj, bool requireSuccess = false)
    {
        if (!typeof(T).GetInterfaces().Any(i => i == typeof(ISystem) || i == typeof(IRevolutionSystem)))
            throw new InvalidOperationException($"{typeof(T)} need to be a ISystem or IRevolutionSystem");

        var systemType = obj.World.GetSystemType<T>();
        Console.WriteLine($"depend on {obj.World.GetSystemHandle(systemType)} (us: {obj.Handle})");
        obj.World.GetComponentData(obj.Handle, obj.DependenciesType)
            .Add(new SystemDependencies
            {
                Other = obj.World.GetSystemHandle(systemType),
                RequireSuccess = requireSuccess
            });
    }

    public static void AddForeignDependency<T>(this SystemObject obj, bool requireSuccess = false)
    {
        if (!typeof(T).GetInterfaces().Any(i => i != typeof(ISystem) && i != typeof(IRevolutionSystem)))
            throw new InvalidOperationException($"{typeof(T)} need to be a ISystem or IRevolutionSystem");

        var systemType = obj.World.GetSystemType<T>();
        obj.World.GetComponentData(obj.World.GetSystemHandle(systemType), obj.DependenciesType)
            .Add(new SystemDependencies
            {
                Other = obj.Handle,
                RequireSuccess = requireSuccess
            });
    }
}