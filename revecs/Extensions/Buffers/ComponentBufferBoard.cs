using Collections.Pooled;
using revecs.Core;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;
using revecs.Utility;

namespace revecs.Extensions.Buffers;

public class ComponentBufferBoard : LinkedComponentBoardBase,
    IComponentBoardHasHandleReader
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

    public Span<byte> Read(in UComponentHandle handle)
    {
        return column.data[handle.Id].Span;
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

    public override void CreateComponent(Span<UComponentHandle> output, Span<byte> data, bool singleData)
    {
        base.CreateComponent(output, data, singleData);
        if (data.Length == 0)
            return;

        if (singleData)
        {
            foreach (var handle in output)
            {
                column.data[handle.Id].AddRange(data);
            }
        }
        else
        {
            throw new InvalidOperationException("does not know");
        }
    }

    public override void DestroyComponent(UComponentHandle handle)
    {
        base.DestroyComponent(handle);
        column.data[handle.Id].Dispose();
    }

    public override bool Support<T>()
    {
        return BufferManagedTypeData<T>.IsBufferData || (base.Support<T>() && !ManagedTypeData<T>.ContainsReference);
    }
}