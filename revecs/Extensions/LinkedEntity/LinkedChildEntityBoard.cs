using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Utility;

namespace revecs.Extensions.LinkedEntity;

public class LinkedChildEntityBoard : EntityComponentBoardBase
{
    private readonly LinkedEntityMainBoard _mainBoard;
    private readonly EntityComponentLinkBoard _componentLinkBoard;

    public LinkedChildEntityBoard(RevolutionWorld world) : base(world)
    {
        _mainBoard = world.GetBoard<LinkedEntityMainBoard>(LinkedEntityMainBoard.BoardName);
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
            _componentLinkBoard.GetColumn(ComponentType)[entity.Id] = EntityComponentLink.Reference(
                new UComponentHandle(entity.Id)
            );
        }
    }

    public override void RemoveComponent(Span<UEntityHandle> entities, Span<bool> removed)
    {
        var nullFakeReference = EntityComponentLink.Reference(default);
        foreach (var entity in entities)
        {
            _componentLinkBoard.GetColumn(ComponentType)[entity.Id] = nullFakeReference;

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
        }
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