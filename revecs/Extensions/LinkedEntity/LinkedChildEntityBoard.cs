using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Utility;

namespace revecs.Extensions.LinkedEntity;

public class LinkedChildEntityBoard : ComponentBoardBase
{
    private readonly LinkedEntityMainBoard _mainBoard;

    public LinkedChildEntityBoard(RevolutionWorld world) : base(world)
    {
        _mainBoard = world.GetBoard<LinkedEntityMainBoard>(LinkedEntityMainBoard.BoardName);
    }

    public override void Dispose()
    {

    }

    public override void AddComponent(UEntityHandle entity, Span<byte> _)
    {
        if (!HasComponentBoard.SetAndGetOld(ComponentType, entity, true))
            World.ArchetypeUpdateBoard.Queue(entity);
    }

    public override void RemoveComponent(UEntityHandle entity)
    {
        if (!HasComponentBoard.SetAndGetOld(ComponentType, entity, false))
            return;
        
        var parents = _mainBoard.column.parents[entity.Id].Span;
        var parentLength = parents.Length;
        for (var ent = 0; ent < parentLength; ent++)
        {
            if (_mainBoard.RemoveLinked(parents[ent], entity))
            {
                ent--;
                parentLength--;
            }
        }
        
        World.ArchetypeUpdateBoard.Queue(entity);
    }

    public override Span<byte> GetComponentData(UEntityHandle handle)
    {
        return MemoryMarshal.Cast<UEntityHandle, byte>(GetComponentData<UEntityHandle>(handle));
    }

    public override Span<T> GetComponentData<T>(UEntityHandle handle)
    {
        return _mainBoard.column.parents[handle.Id].Span.UnsafeCast<UEntityHandle, T>();
    }
}