namespace revecs.Core.Components.Boards.Modifiers
{
    public interface IComponentBoardHasTypeSupport
    {
        public bool Support<T>();
    }
}