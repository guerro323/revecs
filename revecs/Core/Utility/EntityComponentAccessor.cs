using System.Runtime.CompilerServices;
using revecs.Core.Components.Boards.Bases;

namespace revecs.Core;

public ref struct EntityComponentAccessor<T>
{
    public EntityComponentBoardBase Reader;

    public EntityComponentAccessor(EntityComponentBoardBase reader)
    {
        Reader = reader;
    }

    public Span<T> this[UEntityHandle handle] => Reader.GetComponentData<T>(handle);
    public Span<T> this[UEntitySafe safe] => Reader.GetComponentData<T>(safe.Handle);

    public ref T TryGetFirst(UEntityHandle handle)
    {
        if (this[handle].IsEmpty)
            return ref Unsafe.NullRef<T>();

        return ref this[handle][0];
    }
}