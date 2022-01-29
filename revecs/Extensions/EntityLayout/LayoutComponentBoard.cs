using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;

namespace revecs.Extensions.EntityLayout;

public class LayoutComponentBoard : ComponentBoardBase
{
    public readonly ComponentType[] ComponentTypes;
    
    private ArchetypeUpdateBoard _archetypeUpdateBoard;
    private IDisposable _entityResizeEv;

    public LayoutComponentBoard(ComponentType[] componentTypes, RevolutionWorld world) : base(world)
    {
        ComponentTypes = componentTypes;

        _archetypeUpdateBoard = world.GetBoard<ArchetypeUpdateBoard>("ArchetypeUpdate");
        _archetypeUpdateBoard.PreSwitch += OnEntityArchetypePreSwitch;
    }

    private void OnEntityArchetypePreSwitch(Span<UEntityHandle> span)
    {
        foreach (ref var entity in span)
        {
            if (HasComponentBoard.GetColumn(ComponentType)[entity.Id] == false)
                continue;
            
            var removeLayout = false;
            foreach (var componentType in ComponentTypes)
                if (!World.HasComponent(entity, componentType))
                {
                    removeLayout = true;
                    break;
                }

            if (removeLayout)
            {
                Console.WriteLine("removed layout");
                RemoveComponent(entity);
            }
        }
    }

    public override void Dispose()
    {
        _entityResizeEv.Dispose();
        _archetypeUpdateBoard.PreSwitch -= OnEntityArchetypePreSwitch;
    }

    public override void AddComponent(UEntityHandle entity, Span<byte> data)
    {
        var span = ComponentTypes.AsSpan();
        for (var comp = 0; comp < span.Length; comp++)
        {
            if (!World.HasComponent(entity, span[comp]))
                World.AddComponent(entity, span[comp]);
        }

        if (!HasComponentBoard.SetAndGetOld(ComponentType, entity, true))
        {
            World.ArchetypeUpdateBoard.Queue(entity);
        }
    }

    public override void RemoveComponent(UEntityHandle entity)
    {
        var length = ComponentTypes.Length;
        for (var comp = 0; comp < length; comp++)
        {
            World.RemoveComponent(entity, ComponentTypes[comp]);
        }

        if (HasComponentBoard.SetAndGetOld(ComponentType, entity, true))
        {
            World.ArchetypeUpdateBoard.Queue(entity);
        }
    }

    // vvv return data of the layout components?
    
    public override Span<byte> GetComponentData(UEntityHandle handle)
    {
        throw new NotImplementedException();
    }

    public override Span<T> GetComponentData<T>(UEntityHandle handle)
    {
        throw new NotImplementedException();
    }
}