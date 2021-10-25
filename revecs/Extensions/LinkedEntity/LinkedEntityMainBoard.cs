using System.Runtime.CompilerServices;
using Collections.Pooled;
using revecs.Core;
using revecs.Core.Boards;

namespace revecs.Extensions.LinkedEntity;

public class LinkedEntityMainBoard : BoardBase
{
    public const string BoardName = "LinkedEntity";
    
    public (PooledList<UEntityHandle>[] children, PooledList<UEntityHandle>[] parents) column;

    public ComponentType OwnerComponentType { get; internal set; }
    public ComponentType ChildComponentType { get; internal set; }

    private IDisposable _subscribeDisposable;

    public LinkedEntityMainBoard(RevolutionWorld world) : base(world)
    {
        var entityBoard = world.GetBoard<EntityBoard>("Entity");

        _subscribeDisposable = entityBoard.CurrentSize.Subscribe((prev, size) =>
        {
            Array.Resize(ref column.children, size);
            Array.Resize(ref column.parents, size);

            for (; prev < size; prev++)
            {
                column.children[prev] = new PooledList<UEntityHandle>();
                column.parents[prev] = new PooledList<UEntityHandle>();
            }
        }, true);
    }

    public override void Dispose()
    {
        _subscribeDisposable.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AddLinked(UEntityHandle parent, UEntityHandle child)
    {
        World.AddComponent(parent, OwnerComponentType);
        World.AddComponent(child, ChildComponentType);
        
        ref readonly var children = ref column.children[parent.Id];
        if (!children.Contains(child))
        {
            children.Add(child);

            column.parents[child.Id].Add(parent);
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveLinked(UEntityHandle parent, UEntityHandle child)
    {
        column.parents[child.Id].Remove(parent);

        ref readonly var children = ref column.children[parent.Id];
        return children.Remove(child);
    }
}