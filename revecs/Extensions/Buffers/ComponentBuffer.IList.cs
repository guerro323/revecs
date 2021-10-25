using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Collections.Pooled;

namespace revecs.Extensions.Buffers;

public partial struct BufferData<T>
{
    public struct Enumerator
    {
        private readonly PooledList<byte> list;
        private readonly int step;
        private int index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(PooledList<byte> list)
        {
            this.list = list;
            step = Unsafe.SizeOf<T>();
            index = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            var index = this.index + step;
            if (index <= list.Count)
            {
                this.index = index;
                return true;
            }

            return false;
        }

        public ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, T>(ref list.Span[index - step]);
        }
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(backing);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        // todo: remove array copy
        return (IEnumerator<T>) Span.ToArray().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        // todo: remove array copy
        return (IEnumerator<T>) Span.ToArray().GetEnumerator();
    }

    public void Clear()
    {
        backing.Clear();
    }

    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        Span.Slice(arrayIndex).CopyTo(array);
    }

    public bool Remove(T item)
    {
        var index = IndexOf(item);
        if (index < 0)
            return false;

        RemoveAt(IndexOf(item));
        return true;
    }

    public int Count => Span.Length;
    public bool IsReadOnly => false;

    public int IndexOf(T item)
    {
        var span = Span;
        for (var i = 0; i != span.Length; i++)
            if (UnsafeUtility.SameData(ref span[i], ref item))
                return i;

        return -1;
    }

    public void Insert(int index, T item)
    {
        backing.InsertRange(index * Unsafe.SizeOf<T>(),
            MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref item, 1)));
    }

    public void RemoveAt(int index)
    {
        backing.RemoveRange(index * Unsafe.SizeOf<T>(), Unsafe.SizeOf<T>());
    }

    public T this[int index]
    {
        get => Span[index];
        set => Span[index] = value;
    }
}