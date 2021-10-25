using revecs.Core;

namespace revecs.Systems;

public static class DependencyExtensions
{
    public static SwapDependency GetComponentDependency(this RevolutionWorld world, ComponentType type)
    {
        var board = world.GetBoardOrDefault<DependencyBoard>(DependencyBoard.Name);
        if (board is null)
            world.AddBoard(DependencyBoard.Name, board = new DependencyBoard(world));

        return board.Get(type);
    }

    public static SwapDependency GetEntityDependency(this RevolutionWorld world)
    {
        var board = world.GetBoardOrDefault<DependencyBoard>(DependencyBoard.Name);
        if (board is null)
            world.AddBoard(DependencyBoard.Name, board = new DependencyBoard(world));

        return board.GetEntity();
    }
}