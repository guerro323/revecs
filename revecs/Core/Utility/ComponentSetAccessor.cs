using System.Runtime.CompilerServices;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Modifiers;

namespace revecs.Core;

public ref struct ComponentSetAccessor<T>
{
    public Span<EntityComponentLink> ComponentLink;
    public IComponentBoardHasHandleReader Reader;

    public ComponentSetAccessor(Span<EntityComponentLink> componentLink, IComponentBoardHasHandleReader reader)
    {
        ComponentLink = componentLink;
        Reader = reader;
    }

    public Span<T> this[UEntityHandle handle] => Reader.Read<T>(ComponentLink[handle.Id].Handle);
    public Span<T> this[UEntitySafe handle] => Reader.Read<T>(ComponentLink[handle.Row].Handle);

    public bool Contains(UEntityHandle handle) => ComponentLink.Length > handle.Id;
    public bool Contains(UEntitySafe handle) => ComponentLink.Length > handle.Row;
    
    public ref T TryGetFirst(UEntityHandle handle)
    {
        if (!Contains(handle))
            return ref Unsafe.NullRef<T>();
        
        if (this[handle].IsEmpty)
            return ref Unsafe.NullRef<T>();

        return ref this[handle][0];
    }

    public ref T FirstOrThrow(UEntityHandle handle)
    {
        ref var data = ref TryGetFirst(handle);
        if (Unsafe.IsNullRef(ref data))
            throw new NullReferenceException(nameof(data));

        return ref data;
    }
}