using System.Runtime.CompilerServices;
using revecs.Core.Components.Boards.Bases;

namespace revecs.Core;

public ref struct EntityComponentAccessor<T>
{
    public ComponentBoardBase Reader;

    public EntityComponentAccessor(ComponentBoardBase reader)
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

    public ref T FirstOrThrow(UEntityHandle ent)
    {
        ref var val = ref TryGetFirst(ent);
        if (Unsafe.IsNullRef(ref val))
            throw new NullReferenceException($"{typeof(T)} for {ent}");

        return ref val;
    }
}