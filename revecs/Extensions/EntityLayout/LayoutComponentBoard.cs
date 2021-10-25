using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;

namespace revecs.Extensions.EntityLayout;

public class LayoutComponentBoard : EntityComponentBoardBase
{
    public readonly ComponentType[] ComponentTypes;

    // Used to know whether or not to remove non-overriden components.
    private UComponentReference[]?[] _referencesPerEntity;

    private ArchetypeUpdateBoard _archetypeUpdateBoard;
    private EntityComponentLinkBoard _componentLinkBoard;
    
    private IDisposable _entityResizeEv;

    public LayoutComponentBoard(ComponentType[] componentTypes, RevolutionWorld world) : base(world)
    {
        ComponentTypes = componentTypes;

        _archetypeUpdateBoard = world.GetBoard<ArchetypeUpdateBoard>("ArchetypeUpdate");
        _componentLinkBoard = world.GetBoard<EntityComponentLinkBoard>("EntityComponentLink");

        var entityBoard = world.GetBoard<EntityBoard>("Entity");
        _entityResizeEv = entityBoard.CurrentSize.Subscribe((prev, size) =>
        {
            Array.Resize(ref _referencesPerEntity, size);
        }, true);

        _archetypeUpdateBoard.PreSwitch += OnEntityArchetypePreSwitch;
    }

    private void OnEntityArchetypePreSwitch(Span<UEntityHandle> span)
    {
        foreach (ref var entity in span)
        {
            if (_referencesPerEntity[entity.Id] == null) return;

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
                
                Unsafe.SkipInit(out bool removed);
                RemoveComponent(MemoryMarshal.CreateSpan(ref entity, 1), MemoryMarshal.CreateSpan(ref removed, 1));
            }
        }
    }

    public override void Dispose()
    {
        _entityResizeEv.Dispose();
        _archetypeUpdateBoard.PreSwitch -= OnEntityArchetypePreSwitch;
    }

    private static ArrayPool<UComponentReference> GetArrayPool() => ArrayPool<UComponentReference>.Shared;

    public override void AddComponent(Span<UEntityHandle> entities, Span<UComponentReference> output, 
        Span<byte> _0,
        bool _1)
    {
        foreach (ref readonly var entity in entities)
        {
            ref var compReferenceArray = ref _referencesPerEntity[entity.Id];
            compReferenceArray ??= GetArrayPool().Rent(ComponentTypes.Length);

            var span = ComponentTypes.AsSpan();
            for (var comp = 0; comp < span.Length; comp++)
            {
                compReferenceArray[comp] = World.AddComponent(entity, span[comp]);
            }

            if (_componentLinkBoard.AssignComponentReference
            (
                entity,
                new UComponentReference(ComponentType, new UComponentHandle(entity.Id))
            ).Id == 0)
            {
                _archetypeUpdateBoard.Queue(entity);
            }
        }
    }

    public override void RemoveComponent(Span<UEntityHandle> entities, Span<bool> removed)
    {
        var nullReference = new UComponentReference(ComponentType, default);

        for (var ent = 0; ent < entities.Length; ent++)
        {
            ref var compReferenceArray = ref _referencesPerEntity[entities[ent].Id];
            if (compReferenceArray == null)
            {
                removed[ent] = false;
                continue;
            }

            var length = ComponentTypes.Length;
            for (var comp = 0; comp < length; comp++)
            {
                if (_componentLinkBoard.GetColumn(ComponentTypes[comp])[entities[ent].Id].Handle
                    .Equals(compReferenceArray[comp].Handle))
                {
                    World.RemoveComponent(entities[ent], ComponentTypes[comp]);
                }
            }

            if (_componentLinkBoard.AssignComponentReference(entities[ent], nullReference).Id > 0)
                _archetypeUpdateBoard.Queue(entities[ent]);

            removed[ent] = true;
            GetArrayPool().Return(compReferenceArray);
            compReferenceArray = null;
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