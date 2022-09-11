using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Utility;

namespace revecs.Extensions.LinkedEntity;

public class LinkedParentEntityBoard : ComponentBoardBase
{
    private readonly LinkedEntityMainBoard _mainBoard;
    public LinkedParentEntityBoard(RevolutionWorld world) : base(world)
    {
        _mainBoard = world.GetBoard<LinkedEntityMainBoard>(LinkedEntityMainBoard.BoardName);
    }

    public override void Dispose()
    {

    }
    
    public override void AddComponent(UEntityHandle entity, Span<byte> data)
    {
        if (!HasComponentBoard.SetAndGetOld(ComponentType, entity, true))
            World.ArchetypeUpdateBoard.Queue(entity);
    }

    public override void RemoveComponent(UEntityHandle entity)
    {
        if (!HasComponentBoard.SetAndGetOld(ComponentType, entity, false))
            return;
        
        var children = _mainBoard.column.children[entity.Id].Span;
        var childrenLength = children.Length;
        for (var ent = 0; ent < childrenLength; ent++)
        {
            var linkedEntity = children[ent];
            if (World.Exists(linkedEntity))
            {
                World.DestroyEntity(linkedEntity);
                ent--;
                childrenLength--;
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
        return _mainBoard.column.children[handle.Id].Span.UnsafeCast<UEntityHandle, T>();
    }
}