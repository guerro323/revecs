using System.Runtime.CompilerServices;
using revecs.Core.Components;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;

namespace revecs.Core;

public partial class RevolutionWorld
{
#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public ComponentType GetComponentType(ReadOnlySpan<char> name)
    {
        foreach (var row in ComponentTypeBoard.All)
            if (ComponentTypeBoard.Names[row.Handle].AsSpan().SequenceEqual(name))
                return new ComponentType(row.Handle);

        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentType<T> GetComponentType<T>(ReadOnlySpan<char> name)
    {
        return AsGenericComponentType<T>(GetComponentType(name));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentType RegisterComponent(string name, ComponentBoardBase board)
    {
        if (GetComponentType(name).Handle > 0)
            throw new InvalidOperationException($"A component named '{name}' already exist");

        var componentType = ComponentTypeBoard.CreateComponentType(name, board);
        board.SetComponentType(componentType);

        return componentType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentType RegisterComponent<T>(T description = default)
        where T : struct, IComponentSetup
    {
        return description.Create(this);
    }

    public ComponentType<TOut> RegisterComponent<TSetup, TOut>(TSetup description = default)
        where TSetup : struct, IComponentSetup
    {
        return AsGenericComponentType<TOut>(RegisterComponent(description));
    }

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public ComponentType<T> AsGenericComponentType<T>(ComponentType componentType)
    {
        if (componentType.Equals(default))
            return default;
        
        var board = ComponentTypeBoard.Boards[componentType.Handle];
        if (board is IComponentBoardHasTypeSupport typeSupport && !typeSupport.Support<T>())
        {
            var name = ComponentTypeBoard.Names[componentType.Handle];

            throw new InvalidOperationException(
                $"Board '{board.GetType()}' of component type '{name}' doesn't support managed type '{typeof(T)}'"
            );
        }

        return new ComponentType<T>(componentType.Handle);
    }
}