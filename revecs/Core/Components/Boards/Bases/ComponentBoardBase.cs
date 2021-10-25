using revecs.Core.Boards;

namespace revecs.Core.Components.Boards.Bases
{
    public abstract class ComponentBoardBase : BoardBase
    {
        public bool CustomEntityOperation;

        public ComponentBoardBase(RevolutionWorld world) : base(world)
        {
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
    }
}