namespace revecs.Core.Boards
{
    public abstract class BoardBase : IDisposable
    {
        public readonly RevolutionWorld World;

        public BoardBase(RevolutionWorld world)
        {
            World = world;
        }

        public abstract void Dispose();
    }
}