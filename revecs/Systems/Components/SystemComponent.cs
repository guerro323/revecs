using System.Runtime.CompilerServices;
using revecs.Core;
using revecs.Core.Components.Boards;
using revecs.Utility;

namespace revecs.Systems;

public static class SystemComponentExtensions
{
    private static string ComponentName<T>()
    {
        return ManagedTypeData<T>.Name;
    }

    public static void SetSystem<T>(this RevolutionWorld world, UEntityHandle handle)
    {
        var componentType = world.GetComponentType(ComponentName<T>());
        if (componentType.Equals(default))
        {
            componentType = world.RegisterComponent(ComponentName<T>(), new SystemComponentBoard(world));
        }

        world.AddComponent(handle, componentType);
    }

    public static ComponentType GetSystemType<T>(this RevolutionWorld world)
    {
        var componentType = world.GetComponentType(ComponentName<T>());
        if (componentType.Equals(default))
        {
            componentType = world.RegisterComponent(ComponentName<T>(), new SystemComponentBoard(world));
        }

        return componentType;
    }

    public static SystemHandle GetSystemHandle(this RevolutionWorld world, ComponentType type)
    {
        return ((SystemComponentBoard) world.ComponentTypeBoard.Boards[type.Handle]).GetHandle();
    }
}

class SystemComponentBoard : TagComponentBoard
{
    private UEntityHandle _currentHandle;
    
    public SystemComponentBoard(RevolutionWorld world) : base(world)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UEntityHandle GetHandle() => _currentHandle;
    
    public override void AddComponent(Span<UEntityHandle> entities, Span<UComponentReference> output, Span<byte> _0, bool _1)
    {
        if (!_currentHandle.Equals(default))
            throw new InvalidOperationException(
                $"A system can't be present more than one time. Occured on {World.ComponentTypeBoard.Names[ComponentType.Handle]}"
            );
        
        _currentHandle = entities[0];
        
        base.AddComponent(entities, output, _0, _1);
    }

    public override void RemoveComponent(Span<UEntityHandle> entities, Span<bool> removed)
    {
        _currentHandle = default;
        
        base.RemoveComponent(entities, removed);
    }
}