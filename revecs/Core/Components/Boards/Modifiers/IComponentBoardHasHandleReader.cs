namespace revecs.Core.Components.Boards.Modifiers
{
    public interface IComponentBoardHasHandleReader
    {
        Span<byte> Read(in UComponentHandle handle);
        Span<T> Read<T>(in UComponentHandle handle);
    }
}