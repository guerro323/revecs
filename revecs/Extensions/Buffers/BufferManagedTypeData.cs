namespace revecs.Extensions.Buffers;

internal static class BufferManagedTypeData<T>
{
    public static readonly bool IsBufferData;

    static BufferManagedTypeData()
    {
        IsBufferData = false;
        if (typeof(T).IsGenericType && !typeof(T).IsGenericTypeDefinition)
        {
            IsBufferData = typeof(T).GetGenericTypeDefinition() == typeof(BufferData<>);
        }
    }
}