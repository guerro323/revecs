using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace revecs.Utility
{
    public struct IntRowCollection
    {
        private bool[] rowStates;
        private int[] unusedRows;
        private int[] orderedActiveRows;

        public int UnusedCount;

        public int Count;
        public int MaxId;

        public Action<int, int>? OnResize;

        private bool dirtyStart;

        public Span<bool> RowStates => rowStates;

        public Span<int> OrderedActiveRows
        {
            get
            {
                var usedRowSpan = orderedActiveRows.AsSpan(0, Count);
                if (dirtyStart)
                {
                    ref var usedRowRef = ref MemoryMarshal.GetReference(usedRowSpan);
                    ref var rowStateRef = ref MemoryMarshal.GetReference(RowStates);

                    int row;
                    int final;

                    var length = rowStates.Length;
                    for (row = 0, final = 0; row < length && final < Count; row++)
                        if (Unsafe.Add(ref rowStateRef, row))
                            Unsafe.Add(ref usedRowRef, final++) = row;

                    dirtyStart = false;
                }

                return usedRowSpan;
            }
        }

        public IntRowCollection(Action<int, int>? onResize)
        {
            rowStates = Array.Empty<bool>();

            unusedRows = Array.Empty<int>();
            MaxId = 1;

            orderedActiveRows = Array.Empty<int>();
            Count = 0;
            UnusedCount = 0;

            dirtyStart = false;

            OnResize = onResize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Resize<T>(ref T[] array, int length)
        {
            Array.Resize(ref array, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetUnusedRow(int position)
        {
            if (position > MaxId)
                return false;

            unusedRows[UnusedCount++] = position;
            rowStates[position] = false;

            // swapback
            Count--;
            dirtyStart = true;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrySetUnusedRowBulk(Span<int> span)
        {
            ref var unusedRowRef = ref MemoryMarshal.GetReference(unusedRows.AsSpan());
            ref var rowRef = ref MemoryMarshal.GetReference(rowStates.AsSpan());

            var length = span.Length;
            for (var i = 0; i < length; i++)
            {
                var position = span[i];
                if (position > MaxId)
                    continue;

                Unsafe.Add(ref unusedRowRef, UnusedCount++) = position;
                Unsafe.Add(ref rowRef, position) = false;
            }

            Count -= length;
            dirtyStart = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateRowBulk(Span<int> input)
        {
            dirtyStart = true;

            Count += input.Length;

            var id = MaxId;
            var length = input.Length;
            while (UnusedCount > 0 && length-- > 0)
            {
                UnusedCount -= 1;

                // the reason we do not use TryDequeue is to keep the original ID intact,
                // in case we don't have any recycled ids (since calling this method will reset the variable)
                id = unusedRows[UnusedCount];

                rowStates[id] = true;
                input[length] = id;
            }

            if (
                length <= 0) // If it's 0 that mean we've used all unused rows, or if it's less than 0, this mean we created enough rows.
                return;

            MaxId += length; // adding one is necessary here, since ids start from 1
            if (MaxId >= rowStates.Length)
            {
                var prevCount = rowStates.Length;
                var newCount = (MaxId + 1) * 2;

                Resize(ref rowStates, newCount);
                Resize(ref orderedActiveRows, newCount);
                Resize(ref unusedRows, newCount);

                if (OnResize != null)
                    OnResize(prevCount, newCount);
            }

            length--; // necessary for the FOR operation to execute correctly
            for (; id < MaxId; id++)
            {
                rowStates[id] = true;
                input[length--] = id;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CreateRow()
        {
            dirtyStart = true;
            Count += 1;

            if (UnusedCount > 0)
            {
                UnusedCount -= 1;

                var id = unusedRows[UnusedCount];
                rowStates[id] = true;
                return id;
            }

            if (MaxId >= rowStates.Length)
            {
                var prevCount = rowStates.Length;
                var newCount = (MaxId + 1) * 2;

                Resize(ref rowStates, newCount);
                Resize(ref orderedActiveRows, newCount);
                Resize(ref unusedRows, newCount);

                if (OnResize != null)
                    OnResize(prevCount, newCount);
            }

            rowStates[MaxId] = true;
            return MaxId++;
        }

        public ref T GetColumn<T>(int row, ref T[] array)
        {
            if (MaxId >= array.Length)
                Array.Resize(ref array, (MaxId + 1) * 2);
            return ref array[row];
        }

        public void Clear()
        {
            MaxId = 1;

            orderedActiveRows.AsSpan().Clear();
            Count = 0;
            UnusedCount = 0;
        }
    }
}