namespace revecs.Core.Components.Boards.Bases
{
    public abstract class EntityComponentBoardBase : ComponentBoardBase
    {
        public EntityComponentBoardBase(RevolutionWorld world) : base(world)
        {
            CustomEntityOperation = true;
        }

        public abstract void AddComponent(Span<UEntityHandle> entities, Span<UComponentReference> output,
            Span<byte> data, bool singleData);

        public abstract void RemoveComponent(Span<UEntityHandle> entities, Span<bool> removed);

        public abstract Span<byte> GetComponentData(UEntityHandle handle);
        public abstract Span<T> GetComponentData<T>(UEntityHandle handle);
    }
}