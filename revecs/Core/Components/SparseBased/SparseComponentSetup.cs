using revecs.Core.Components.Boards;
using revecs.Utility;

namespace revecs.Core.Components;

public struct SparseComponentSetup<T> : IComponentSetup
{
    public ComponentType Create(RevolutionWorld revolutionWorld)
    {
        if (!ManagedTypeData<T>.IsValueType)
            throw new InvalidOperationException(
                $"{typeof(T)} need to be a struct"
            );

        if (ManagedTypeData<T>.Size == 0)
            return revolutionWorld.RegisterComponent(
                ManagedTypeData<T>.Name,
                new TagComponentBoard(revolutionWorld)
            );
            
        if (ManagedTypeData<T>.ContainsReference)
            return revolutionWorld.RegisterComponent(
                ManagedTypeData<T>.Name,
                new SparseSetManagedComponentBoard<T>(ManagedTypeData<T>.Size, revolutionWorld)
            );
        return revolutionWorld.RegisterComponent(
            ManagedTypeData<T>.Name,
            new SparseSetComponentBoard(ManagedTypeData<T>.Size, revolutionWorld)
        );
    }
}