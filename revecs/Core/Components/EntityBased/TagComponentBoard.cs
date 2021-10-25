using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;

namespace revecs.Core.Components.Boards;

public class TagComponentBoard : EntityComponentBoardBase
{
    private EntityComponentLinkBoard _componentLinkBoard;

    public TagComponentBoard(RevolutionWorld world) : base(world)
    {
        _componentLinkBoard = world.GetBoard<EntityComponentLinkBoard>("EntityComponentLink");
    }

    public override void Dispose()
    {
    }

    public override void AddComponent(Span<UEntityHandle> entities, Span<UComponentReference> output,
        Span<byte> _0, bool _1)
    {
        var validReference = new UComponentReference(ComponentType, new UComponentHandle(1));
        foreach (ref readonly var entity in entities)
            _componentLinkBoard.AssignComponentReference(entity, validReference);

        foreach (ref var result in output)
            result = validReference;
    }

    public override void RemoveComponent(Span<UEntityHandle> entities, Span<bool> removed)
    {
        var nullReference = new UComponentReference(ComponentType, default);
        for (var i = 0; i < entities.Length; i++)
        {
            removed[i] = _componentLinkBoard.AssignComponentReference(entities[i], nullReference).Id != 0;
        }
    }

    public override Span<byte> GetComponentData(UEntityHandle handle)
    {
        return Span<byte>.Empty;
    }

    public override Span<T> GetComponentData<T>(UEntityHandle handle)
    {
        return Span<T>.Empty;
    }
}