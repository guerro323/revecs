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

    public static BufferData<T> ReadBuffer<T>(this RevolutionWorld world, UEntityHandle handle,
        BufferType<T> bufferType)
        where T : struct
    {
        if (world.ComponentTypeBoard.Boards[bufferType.ComponentType.Handle] is not ComponentBufferBoard board)
            throw new InvalidCastException(nameof(board));

        var componentRef = board.EntityLink[handle.Id];
        if (componentRef.Id == 0)
            throw new InvalidOperationException();

        return new BufferData<T>(board.column.data[componentRef.Id]);
    }
}