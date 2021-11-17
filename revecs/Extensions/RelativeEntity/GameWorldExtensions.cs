using revecs.Core;

namespace revecs.Extensions.RelativeEntity;

public static class GameWorldExtensions
{
    private const string MainBoardName = RelativeEntityMainBoard.BoardName;

    public static void AddRelativeEntityModule(this RevolutionWorld world)
    {
        if (world.GetBoardOrDefault(MainBoardName) != null)
            return;

        var mainBoard = new RelativeEntityMainBoard(world);
        world.AddBoard(MainBoardName, mainBoard);
    }

    public static DescriptionType RegisterDescription(this RevolutionWorld world, string name)
    {
        var mainBoard = world.GetBoard<RelativeEntityMainBoard>(MainBoardName);
        return GetDescriptionType(world, mainBoard.Register(name));
    }

    /// <summary>
    /// Convert a component type (can be a description or a relative target) to a <see cref="DescriptionType"/>
    /// </summary>
    /// <param name="componentType">Component Type to convert</param>
    /// <returns>The DescriptionType</returns>
    public static DescriptionType GetDescriptionType(this RevolutionWorld world, ComponentType componentType)
    {
        var mainBoard = world.GetBoard<RelativeEntityMainBoard>(MainBoardName);
        if (mainBoard.ChildToBaseType[componentType.Handle].Equals(default) == false)
        {
            return new DescriptionType(
                mainBoard.ChildToBaseType[componentType.Handle],
                componentType
            );
        }

        if (mainBoard.ChildComponentType[componentType.Handle].Equals(default) == false)
        {
            return new DescriptionType(
                componentType,
                mainBoard.ChildComponentType[componentType.Handle]
            );
        }

        return default;
    }

    public static void AddRelative(this RevolutionWorld world,
        DescriptionType type, UEntityHandle handle, UEntityHandle relative)
    {
        world.AddComponent(handle, type.Relative, relative);
    }

    public static void RemoveRelative(this RevolutionWorld world,
        DescriptionType type, UEntityHandle handle)
    {
        world.RemoveComponent(handle, type.Relative);
    }

    public static bool TryGetRelative(this RevolutionWorld world,
        DescriptionType type, UEntityHandle child, out UEntityHandle parent)
    {
        var parentSpan = world.ReadComponent<UEntityHandle>(child, type.Relative);
        if (parentSpan.Length == 0)
        {
            parent = default;
            return false;
        }

        parent = parentSpan[0];
        return true;
    }

    public static Span<UEntityHandle> ReadOwnedRelatives(this RevolutionWorld world,
        DescriptionType type, UEntityHandle owner)
    {
        return world.ReadComponent<UEntityHandle>(owner, type.Itself);
    }
}