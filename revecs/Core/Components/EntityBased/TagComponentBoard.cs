using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;

namespace revecs.Core.Components.Boards;

public class TagComponentBoard : ComponentBoardBase
{
    public TagComponentBoard(RevolutionWorld world) : base(world)
    {
    }

    public override void Dispose()
    {
    }

    public override void AddComponent(UEntityHandle handle, Span<byte> data)
    {
        ref var hasComponent = ref HasComponentBoard.GetColumn(ComponentType)[handle.Id];
        if (!hasComponent)
        {
            hasComponent = true;
            World.ArchetypeUpdateBoard.Queue(handle);
        }
    }

    public override void RemoveComponent(UEntityHandle handle)
    {
        ref var hasComponent = ref HasComponentBoard.GetColumn(ComponentType)[handle.Id];
        if (hasComponent)
        {
            hasComponent = false;
            World.ArchetypeUpdateBoard.Queue(handle);
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