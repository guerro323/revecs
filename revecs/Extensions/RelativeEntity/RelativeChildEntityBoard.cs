using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Utility;

namespace revecs.Extensions.RelativeEntity;

public class RelativeChildEntityBoard : EntityComponentBoardBase
{
    private readonly RelativeEntityMainBoard _mainBoard;
    private readonly EntityComponentLinkBoard _componentLinkBoard;

    public RelativeChildEntityBoard(RevolutionWorld world) : base(world)
    {
        _mainBoard = world.GetBoard<RelativeEntityMainBoard>(RelativeEntityMainBoard.BoardName);
        _componentLinkBoard = world.EntityComponentLinkBoard;
    }

    public ComponentType DescriptionType { get; init; }

    public override void Dispose()
    {

    }

    public override void AddComponent(Span<UEntityHandle> entities,
        Span<UComponentReference> _0, Span<byte> data, bool singleData)
    {
        var parentSpan = data.UnsafeCast<byte, UEntityHandle>();
        if (parentSpan.Length == 0)
        {
            // don't add the component
            return;
        }
        
        if (singleData)
        {
            foreach (var entity in entities)
            {
                _componentLinkBoard.GetColumn(ComponentType)[entity.Id] = EntityComponentLink.Reference(
                    new UComponentHandle(entity.Id)
                );
                _mainBoard.SetLinked(DescriptionType, parentSpan[0], entity);
            }
        }
        else
        {
            for (var ent = 0; ent < entities.Length; ent++)
            {
                _componentLinkBoard.GetColumn(ComponentType)[entities[ent].Id] = EntityComponentLink.Reference(
                    new UComponentHandle(entities[ent].Id)
                );
                _mainBoard.SetLinked(DescriptionType, parentSpan[ent], entities[ent]);
            }
        }
    }

    public override void RemoveComponent(Span<UEntityHandle> entities, Span<bool> removed)
    {
        var nullFakeReference = EntityComponentLink.Reference(default);
        foreach (var entity in entities)
        {
            _componentLinkBoard.GetColumn(ComponentType)[entity.Id] = nullFakeReference;
            _mainBoard.SetLinked(DescriptionType, default, entity);
        }
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