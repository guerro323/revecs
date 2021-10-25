using Collections.Pooled;

namespace revecs.Extensions.Buffers;

public struct BufferDataNonGeneric
{
    public bool IsCreated => Backing != null!;
    public readonly PooledList<byte> Backing;

    public BufferDataNonGeneric(PooledList<byte> backing)
    {
        Backing = backing;
    }
}