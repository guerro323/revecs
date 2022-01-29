using System.Runtime.CompilerServices;
using revecs.Core.Components.Boards.Bases;
using revghost.Shared.Events;

namespace revecs.Core.Boards;

public class EntityHasComponentBoard : BoardBase
{
    public bool[][] EntityHasComponentColumn;
    
    private readonly BindableListener _componentTypeResizeListener;

    private readonly BindableListener _entityResizeListener;

    private int _lastEntitySize;

    public EntityHasComponentBoard(RevolutionWorld world) : base(world)
    {
        EntityHasComponentColumn = Array.Empty<bool[]>();

        var entityBoard = world.GetBoard<EntityBoard>("Entity");
        var componentTypeBoard = world.GetBoard<ComponentTypeBoard>("ComponentType");

        _entityResizeListener = entityBoard.CurrentSize.Subscribe(EntityOnResize, true);
        _componentTypeResizeListener = componentTypeBoard.CurrentSize.Subscribe(ComponentTypeOnResize, true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<bool> GetColumn(ComponentType type)
    {
        return EntityHasComponentColumn[type.Handle];
    }

    private void EntityOnResize(int prev, int curr)
    {
        foreach (ref var column in EntityHasComponentColumn.AsSpan()) Array.Resize(ref column, curr);

        _lastEntitySize = curr;
    }

    private void ComponentTypeOnResize(int _, int curr)
    {
        var previousSize = EntityHasComponentColumn.Length;

        Array.Resize(ref EntityHasComponentColumn, curr);

        foreach (ref var column in EntityHasComponentColumn.AsSpan(previousSize))
            column = new bool[_lastEntitySize];
    }

    public override void Dispose()
    {
        _entityResizeListener.Dispose();
        _componentTypeResizeListener.Dispose();
    }

    public bool SetAndGetOld(ComponentType type, UEntityHandle handle, bool newValue)
    {
        ref var reference = ref EntityHasComponentColumn[type.Handle][handle.Id];
        var cpy = reference;
        reference = newValue;

        return cpy;
    }
}