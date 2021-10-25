using revecs.Core;

namespace revecs.Extensions.Buffers;

public static class GameWorldExtensions
{
    public static BufferType<T> AsBufferType<T>(this RevolutionWorld world, ComponentType componentType)
        where T : struct
    {
        // checks
        return new BufferType<T>(componentType);
    }

    public static BufferData<T> ReadBuffer<T>(this RevolutionWorld world, UEntityHandle handle, BufferType<T> bufferType)
        where T : struct
    {
        var board = world.ComponentTypeBoard.Boards[bufferType.ComponentType.Handle] as ComponentBufferBoard;
        if (board == null)
            throw new InvalidCastException(nameof(board));

        var componentRef = world.EntityComponentLinkBoard.GetColumn(bufferType.ComponentType)[handle.Id];
        if (componentRef.Null)
            throw new InvalidOperationException();

        return new BufferData<T>(board.column.data[componentRef.Assigned]);
    }
}