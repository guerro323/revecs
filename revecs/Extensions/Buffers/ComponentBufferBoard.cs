using Collections.Pooled;
using revecs.Core;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;
using revecs.Utility;

namespace revecs.Extensions.Buffers;

public class ComponentBufferBoard : LinkedComponentBoardBase
{
    internal (PooledList<byte>[] data, BufferDataNonGeneric[] helper, byte h) column;

    public ComponentBufferBoard(int size, RevolutionWorld world) : base(size, world)
    {
        CurrentSize.Subscribe((prev, next) =>
        {
            Array.Resize(ref column.data, next);
            Array.Resize(ref column.helper, next);
            for (; prev < next; prev++)
            {
                column.data[prev] = new PooledList<byte>();
                column.helper[prev] = new BufferDataNonGeneric(column.data[prev]);
            }
        }, true);
    }

    public Span<T> Read<T>(in UComponentHandle handle)
    {
        if (BufferManagedTypeData<T>.IsBufferData)
        {
            ref var buffer = ref column.helper[handle.Id];

            return buffer
                .ToSpan()
                .UnsafeCast<BufferDataNonGeneric, T>();
        }

        return column.data[handle.Id].Span.UnsafeCast<byte, T>();
    }

    public override void Dispose()
    {
        foreach (var data in column.data)
            data.Dispose();
    }
    
    public override bool Support<T>()
    {
        return BufferManagedTypeData<T>.IsBufferData || (base.Support<T>() && !ManagedTypeData<T>.ContainsReference);
    }

    public override void AddComponent(UEntityHandle handle, Span<byte> data)
    {
        ref var component = ref BaseAddComponent(handle);
        column.data[component.Id].AddRange(data);
    }

    public override void RemoveComponent(UEntityHandle handle)
    {
        var component = BaseRemoveComponent(handle);
        column.data[component.Id].Dispose();
    }

    public override Span<byte> GetComponentData(UEntityHandle handle)
    {
        return column.data[EntityLink[handle.Id].Id].Span;
    }

    public override Span<T> GetComponentData<T>(UEntityHandle handle)
    {
        var component = EntityLink[handle.Id].Id;
        if (BufferManagedTypeData<T>.IsBufferData)
        {
            ref var buffer = ref column.helper[component];

            return buffer
                .ToSpan()
                .UnsafeCast<BufferDataNonGeneric, T>();
        }

        return column.data[component].Span.UnsafeCast<byte, T>();
    }
}