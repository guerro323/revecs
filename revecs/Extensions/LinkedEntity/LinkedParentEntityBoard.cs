using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Utility;

namespace revecs.Extensions.LinkedEntity;

public class LinkedParentEntityBoard : EntityComponentBoardBase
{
    private readonly LinkedEntityMainBoard _mainBoard;
    private readonly EntityComponentLinkBoard _componentLinkBoard;

    public LinkedParentEntityBoard(RevolutionWorld world) : base(world)
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
        }
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