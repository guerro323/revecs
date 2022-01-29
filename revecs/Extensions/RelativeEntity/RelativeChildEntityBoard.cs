using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Utility;

namespace revecs.Extensions.RelativeEntity;

public class RelativeChildEntityBoard : ComponentBoardBase
{
    private readonly RelativeEntityMainBoard _mainBoard;

    public RelativeChildEntityBoard(RevolutionWorld world) : base(world)
    {
        _mainBoard = world.GetBoard<RelativeEntityMainBoard>(RelativeEntityMainBoard.BoardName);
    }

    public ComponentType DescriptionType { get; init; }

    public override void Dispose()
    {

    }

    public override void AddComponent(UEntityHandle handle, Span<byte> data)
    {
        var parentSpan = data.UnsafeCast<byte, UEntityHandle>();
        if (parentSpan.Length == 0)
        {
            // don't add the component
            return;
        }

        if (!HasComponentBoard.SetAndGetOld(ComponentType, handle, true))
            World.ArchetypeUpdateBoard.Queue(handle);

        foreach (var parent in parentSpan)
            _mainBoard.SetLinked(DescriptionType, parent, handle);
    }

    public override void RemoveComponent(UEntityHandle handle)
    {
        if (!HasComponentBoard.SetAndGetOld(ComponentType, handle, false))
            return;

        _mainBoard.SetLinked(DescriptionType, default, handle);
        World.ArchetypeUpdateBoard.Queue(handle);
    }

    public override Span<byte> GetComponentData(UEntityHandle handle)
    {
        return MemoryMarshal.Cast<UEntityHandle, byte>(GetComponentData<UEntityHandle>(handle));
    }

    public override Span<T> GetComponentData<T>(UEntityHandle handle)
    {
        return _mainBoard.columns[DescriptionType.Handle].parent[handle.Id].ToSpan().UnsafeCast<UEntityHandle, T>();
    }
}