using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Utility;

namespace revecs.Extensions.RelativeEntity;

public class RelativeParentEntityBoard : ComponentBoardBase
{
    private readonly RelativeEntityMainBoard _mainBoard;

    public RelativeParentEntityBoard(RevolutionWorld world) : base(world)
    {
        _mainBoard = world.GetBoard<RelativeEntityMainBoard>(RelativeEntityMainBoard.BoardName);
    }

    public override void Dispose()
    {

    }
    
    public override void AddComponent(UEntityHandle handle, Span<byte> data)
    {
        if (!HasComponentBoard.SetAndGetOld(ComponentType, handle, true))
            World.ArchetypeUpdateBoard.Queue(handle);
    }

    public override void RemoveComponent(UEntityHandle handle)
    {
        var column = _mainBoard.columns[ComponentType.Handle];
        var list = column.children[handle.Id];
        while (list.Count > 0)
            _mainBoard.SetLinked(ComponentType, default, list[^1]);
            
        World.ArchetypeUpdateBoard.Queue(handle);
    }

    public override Span<byte> GetComponentData(UEntityHandle handle)
    {
        return MemoryMarshal.Cast<UEntityHandle, byte>(GetComponentData<UEntityHandle>(handle));
    }

    public override Span<T> GetComponentData<T>(UEntityHandle handle)
    {
        return _mainBoard.columns[ComponentType.Handle].children[handle.Id].Span.UnsafeCast<UEntityHandle, T>();
    }
}