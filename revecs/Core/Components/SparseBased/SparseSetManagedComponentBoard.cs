using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;

namespace revecs.Core.Components.Boards
{
    public class SparseSetManagedComponentBoard<T> : LinkedComponentBoardBase, IComponentBoardHasHandleReader,
        IComponentBoardHasGlobalReader
    {
        private (T[] data, byte h) column;

        public SparseSetManagedComponentBoard(int size, RevolutionWorld world) : base(size, world)
        {
            CurrentSize.Subscribe((_, next) => { Array.Resize(ref column.data, next * ComponentByteSize); }, true);
        }

        public Span<byte> Read()
        {
            return MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(column.data.AsSpan())),
                column.data.Length * ComponentByteSize
            );
        }

        public Span<TOther> Read<TOther>()
        {
            return MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, TOther>(ref MemoryMarshal.GetReference(column.data.AsSpan())),
                column.data.Length
            );
        }

        public Span<byte> Read(in UComponentHandle handle)
        {
            return MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(column.data.AsSpan())),
                column.data.Length * ComponentByteSize
            ).Slice(handle.Id * ComponentByteSize, ComponentByteSize);
        }

        public Span<TOther> Read<TOther>(in UComponentHandle handle)
        {
            return MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, TOther>(ref MemoryMarshal.GetReference(column.data.AsSpan())),
                1
            );
        }

        public override void Dispose()
        {
        }

        public override void CreateComponent(Span<UComponentHandle> output, Span<byte> data, bool singleData)
        {
            base.CreateComponent(output, data, singleData);
            if (singleData)
            {
                foreach (var handle in output)
                    data.CopyTo(Read(handle));
            }
            else
            {
                for (var i = 0; i < output.Length; i++)
                {
                    data[(i * ComponentByteSize)..].CopyTo(Read(output[i]));
                }
            }
        }
    }
}