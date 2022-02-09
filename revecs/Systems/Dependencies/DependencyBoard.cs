using System.Runtime.CompilerServices;
using revecs.Core;
using revecs.Core.Boards;

namespace revecs.Systems;

public class DependencyBoard : BoardBase
{
    public const string Name = "DependencyComponent";
    
    private SwapDependency[] column = Array.Empty<SwapDependency>();
    private SwapDependency entity;
    private SwapDependency world;

    private IDisposable _subscribeDisposable;

    public DependencyBoard(RevolutionWorld world) : base(world)
    {
        var board = world.ComponentTypeBoard;

        _subscribeDisposable = board.CurrentSize.Subscribe((_, next) =>
        {
            var prev = column.Length;

            Array.Resize(ref column, next);
            for (; prev < next; prev++)
            {
                column[prev] = new SwapDependency();
            }
        }, true);

        entity = new SwapDependency();
        this.world = new SwapDependency();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SwapDependency Get(ComponentType componentType)
    {
        if (componentType.Handle >= column.Length)
            throw new IndexOutOfRangeException(
                $"Expected an existing component type (got '{componentType.Handle}' but limit is '{column.Length}')"
            );
            
        return column[componentType.Handle];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SwapDependency GetEntity()
    {
        return entity;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SwapDependency GetWorld()
    {
        return world;
    }

    public override void Dispose()
    {
        _subscribeDisposable.Dispose();
    }
}