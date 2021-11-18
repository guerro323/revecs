using System.Runtime.CompilerServices;
using Collections.Pooled;
using revecs.Core;
using revecs.Core.Boards;

namespace revecs.Extensions.RelativeEntity;

public class RelativeEntityMainBoard : BoardBase
{
    public const string BoardName = "RelativeEntity";

    public (PooledList<UEntityHandle>[] children, UEntityHandle[] parent)[] columns;

    //public ComponentType[] OwnerComponentType;
    public ComponentType[] ChildComponentType;
    public ComponentType[] ChildToBaseType;

    private IDisposable _typeResizeEv;
    private IDisposable _entityResizeEv;

    public RelativeEntityMainBoard(RevolutionWorld world) : base(world)
    {
        var entityBoard = world.GetBoard<EntityBoard>("Entity");

        void OnEntityResize(int prev, int size)
        {
            foreach (ref var column in columns.AsSpan())
            {
                Array.Resize(ref column.children, size);
                Array.Resize(ref column.parent, size);

                for (var start = prev; start < size; start++)
                {
                    column.children[start] = new PooledList<UEntityHandle>();
                    column.parent[start] = default;
                }
            }
        }

        _entityResizeEv = entityBoard.CurrentSize.Subscribe(OnEntityResize);

        var componentTypeBoard = world.GetBoard<ComponentTypeBoard>("ComponentType");
        _typeResizeEv = componentTypeBoard.CurrentSize.Subscribe((prev, size) =>
        {
            //Array.Resize(ref OwnerComponentType, size);
            Array.Resize(ref ChildComponentType, size);
            Array.Resize(ref ChildToBaseType, size);
            Array.Resize(ref columns, size);
            
            for (; prev < size; prev++)
            {
                ref var column = ref columns[prev];
                
                var entitySize = entityBoard.CurrentSize.Value;
                Array.Resize(ref column.children, entitySize);
                Array.Resize(ref column.parent, entitySize);
                
                for (var i = 0; i < entitySize; i++)
                {
                    column.children[i] = new PooledList<UEntityHandle>();
                    column.parent[i] = default;
                }
            }
        }, true);
    }

    public override void Dispose()
    {
        _typeResizeEv.Dispose();
        _entityResizeEv.Dispose();
    }

    public bool SetLinked(ComponentType type, UEntityHandle parent, UEntityHandle child)
    {
        ref var currentParent = ref columns[type.Handle].parent[child.Id];
        if (currentParent.Id != parent.Id)
        {
            if (currentParent.Id > 0)
                columns[type.Handle].children[currentParent.Id].Remove(child);

            currentParent = parent;
        }

        if (parent.Id <= 0)
            return false;
        
        World.AddComponent(parent, type);
        World.AddComponent(child, ChildComponentType[type.Handle]);

        ref readonly var children = ref columns[type.Handle].children[parent.Id];
        if (children.Contains(child))
            return false;

        children.Add(child);
        return true;
    }

    public ComponentType Register(string name)
    {
        var type = World.RegisterComponent(
            name,
            new RelativeParentEntityBoard(World)
        );
        
        var childType = World.RegisterComponent(
            name + ":Target",
            new RelativeChildEntityBoard(World) {DescriptionType = type}
        );

        ChildComponentType[type.Handle] = childType;

        ChildToBaseType[ChildComponentType[type.Handle].Handle] = type;
        
        return type;
    }
}