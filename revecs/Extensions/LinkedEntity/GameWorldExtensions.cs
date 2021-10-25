using revecs.Core;

namespace revecs.Extensions.LinkedEntity;

public static class GameWorldExtensions
{
    private const string MainBoardName = LinkedEntityMainBoard.BoardName;
    
    // ReSharper disable MemberCanBePrivate.Global
    public const string ChildrenComponent = "LinkedEntity:Children";
    public const string ParentComponent = "LinkedEntity:Parent";
    // ReSharper restore MemberCanBePrivate.Global

    public static void AddLinkedEntityModule(this RevolutionWorld world)
    {
        if (world.GetBoardOrDefault(MainBoardName) != null)
            return;
        
        var mainBoard = new LinkedEntityMainBoard(world);
        world.AddBoard(MainBoardName, mainBoard);

        mainBoard.ChildComponentType = world.RegisterComponent(ChildrenComponent, new LinkedChildEntityBoard(world));
        mainBoard.OwnerComponentType = world.RegisterComponent(ParentComponent, new LinkedParentEntityBoard(world));
    }

    public static void SetLink(this RevolutionWorld world, UEntityHandle child, UEntityHandle owner, bool isLinked)
    {
        var board = world.GetBoard<LinkedEntityMainBoard>(MainBoardName);
        if (isLinked)
            board.AddLinked(owner, child);
        else
            board.RemoveLinked(owner, child);
    }

    public static Span<UEntityHandle> ReadParents(this RevolutionWorld world, UEntityHandle child)
    {
        var board = world.GetBoard<LinkedEntityMainBoard>(MainBoardName);
        if (!world.HasComponent(child, board.ChildComponentType))
            return Span<UEntityHandle>.Empty;
        
        return world.ReadComponent<UEntityHandle>(child, board.ChildComponentType);
    }
    
    public static Span<UEntityHandle> ReadChildren(this RevolutionWorld world, UEntityHandle child)
    {
        var board = world.GetBoard<LinkedEntityMainBoard>(MainBoardName);
        if (!world.HasComponent(child, board.OwnerComponentType))
            return Span<UEntityHandle>.Empty;
        
        return world.ReadComponent<UEntityHandle>(child, board.OwnerComponentType);
    }
}