using System.Runtime.CompilerServices;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;

namespace revecs.Core;

public ref struct SparseSetAccessor<T>
{
    public Span<LinkedComponentBoardBase.UComponentHandle> EntityLink;
    public Span<T> Data;

    public SparseSetAccessor(Span<LinkedComponentBoardBase.UComponentHandle> entityLink, Span<T> data)
    {
        EntityLink = entityLink;
        Data = data;
    }

    public ref T this[UEntityHandle handle]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Data[EntityLink[handle.Id].Id];
    }

    public ref T this[UEntitySafe handle]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Data[EntityLink[handle.Row].Id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(UEntityHandle handle) => EntityLink.Length > handle.Id && EntityLink[handle.Id].Id != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(UEntitySafe handle) => EntityLink.Length > handle.Row && EntityLink[handle.Row].Id != 0;
}