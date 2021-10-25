using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Utility;

namespace revecs.Extensions.RelativeEntity;

public class RelativeParentEntityBoard : EntityComponentBoardBase
{
    private readonly RelativeEntityMainBoard _mainBoard;
    private readonly EntityComponentLinkBoard _componentLinkBoard;

    public RelativeParentEntityBoard(RevolutionWorld world) : base(world)
    {
        _mainBoard = world.GetBoard<RelativeEntityMainBoard>(RelativeEntityMainBoard.BoardName);
        _componentLinkBoard = world.GetBoard<EntityComponentLinkBoard>("EntityComponentLink");
    }

    public override void Dispose()
    {

    }

    public override void AddComponent(Span<UEntityHandle> entities,
        Span<UComponentReference> _0, Span<byte> _1, bool _2)
    {
        foreach (var entity in entities)
        {
            _componentLinkBoard.GetColumn(ComponentType)[entity.Id] = EntityComponentLink.Reference
            (
                new UComponentHandle(entity.Id)
            );
        }
    }

    public override void RemoveComponent(Span<UEntityHandle> entities, Span<bool> removed)
    {
        var column = _mainBoard.columns[ComponentType.Handle];
        
        var nullFakeReference = EntityComponentLink.Reference(default);
        foreach (var entity in entities)
        {
            _componentLinkBoard.GetColumn(ComponentType)[entity.Id] = nullFakeReference;

            var list = column.children[entity.Id];
            while (list.Count > 0)
                _mainBoard.SetLinked(ComponentType, default, list[^1]);
        }
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