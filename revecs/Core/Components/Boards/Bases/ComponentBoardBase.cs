using revecs.Core.Boards;

namespace revecs.Core.Components.Boards.Bases
{
    public abstract class ComponentBoardBase : BoardBase
    {
        protected EntityHasComponentBoard HasComponentBoard;
        
        public ComponentBoardBase(RevolutionWorld world) : base(world)
        {
            HasComponentBoard = world.EntityHasComponentBoard;
        }

        public ComponentType ComponentType { get; private set; }

        public void SetComponentType(ComponentType type)
        {
            CustomSetComponentType(ref type);
            ComponentType = type;
        }

        protected virtual void CustomSetComponentType(ref ComponentType type)
        {
        }

        public abstract void AddComponent(UEntityHandle handle, Span<byte> data);

        public abstract void RemoveComponent(UEntityHandle handle);

        public abstract Span<byte> GetComponentData(UEntityHandle handle);
        public abstract Span<T> GetComponentData<T>(UEntityHandle handle);
    }
}