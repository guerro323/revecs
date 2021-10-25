namespace revecs.Core.Components.Boards.Modifiers
{
    public interface IComponentBoardHasGlobalReader
    {
        Span<byte> Read();

        Span<T> Read<T>();
    }
}