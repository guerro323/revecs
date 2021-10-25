using System.Buffers;

namespace revecs
{
    public struct DisposableArray<T> : IDisposable
    {
        private T[] array;

        public static DisposableArray<T> Rent(int size, out T[] bytes)
        {
            return new DisposableArray<T> {array = bytes = ArrayPool<T>.Shared.Rent(size)};
        }

        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(array);
        }
    }
}