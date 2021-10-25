using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace revecs.Utility
{
    public struct UIntRowCollection
    {
        private bool[] rowStates;
        private uint[] unusedRows;
        private uint[] orderedActiveRows;

        public int UnusedCount;

        public int Count;
        public uint MaxId;

        public Action<int, int>? OnResize;

        private bool dirtyStart;

        public Span<bool> RowStates => rowStates;

        public Span<uint> OrderedActiveRows
        {
            get
            {
                var usedRowSpan = orderedActiveRows.AsSpan(0, Count);
                if (dirtyStart)
                {
                    ref var usedRowRef = ref MemoryMarshal.GetReference(usedRowSpan);

                    uint row;
                    int final;

                    var length = rowStates.Length;
                    for (row = 0, final = 0; row < length && final < Count; row++)
                        if (rowStates[row])
                            Unsafe.Add(ref usedRowRef, final++) = row;

                    dirtyStart = false;
                }

                return usedRowSpan;
            }
        }

        public UIntRowCollection(Action<int, int>? onResize)
        {
            rowStates = Array.Empty<bool>();

            unusedRows = Array.Empty<uint>();
            MaxId = 1;

            orderedActiveRows = Array.Empty<uint>();
            Count = 0;
            UnusedCount = 0;

            dirtyStart = false;

            OnResize = onResize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetUnusedRow(uint position)
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
        public void TrySetUnusedRowBulk(Span<uint> span)
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
                Unsafe.Add(ref rowRef, (int) position) = false;
            }

            Count -= length;
            dirtyStart = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateRowBulk(Span<uint> input)
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

            MaxId += (uint) length; // adding one is necessary here, since ids start from 1
            if (MaxId >= rowStates.Length)
            {
                var prevCount = rowStates.Length;
                var newCount = (int) (MaxId + 1) * 2;

                Array.Resize(ref rowStates, newCount);
                Array.Resize(ref orderedActiveRows, newCount);
                Array.Resize(ref unusedRows, newCount);

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
        public uint CreateRow()
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
                Array.Resize(ref rowStates, (int) (MaxId + 1) * 2);
                Array.Resize(ref orderedActiveRows, (int) (MaxId + 1) * 2);
                Array.Resize(ref unusedRows, (int) (MaxId + 1) * 2);
            }

            rowStates[MaxId] = true;
            return MaxId++;
        }

        public ref T GetColumn<T>(uint row, ref T[] array)
        {
            if (MaxId >= array.Length)
                Array.Resize(ref array, (int) ((MaxId + 1) * 2));
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