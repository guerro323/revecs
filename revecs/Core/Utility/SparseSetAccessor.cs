using System.Runtime.CompilerServices;
using revecs.Core.Boards;

namespace revecs.Core;

public ref struct SparseSetAccessor<T>
{
    public Span<EntityComponentLink> ComponentLink;
    public Span<T> Data;

    public SparseSetAccessor(Span<EntityComponentLink> componentLink, Span<T> data)
    {
        ComponentLink = componentLink;
        Data = data;
    }

    public ref T this[UEntityHandle handle]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Data[ComponentLink[handle.Id].Assigned];
    }

    public ref T this[UEntitySafe handle]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Data[ComponentLink[handle.Row].Assigned];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(UEntityHandle handle) => ComponentLink.Length > handle.Id && ComponentLink[handle.Id].Valid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(UEntitySafe handle) => ComponentLink.Length > handle.Row && ComponentLink[handle.Row].Valid;
}