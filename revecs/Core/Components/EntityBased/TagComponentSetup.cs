using revecs.Core.Components.Boards;
using revecs.Utility;

namespace revecs.Core.Components;

public struct TagComponentSetup<T> : IComponentSetup
{
    public ComponentType Create(RevolutionWorld revolutionWorld)
    {
        return revolutionWorld.RegisterComponent(
            ManagedTypeData<T>.Name,
            new TagComponentBoard(revolutionWorld)
        );
    }
}