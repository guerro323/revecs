using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;
using revecs.Utility;

namespace revecs.Core.Components.Boards
{
    public class SparseSetComponentBoard : LinkedComponentBoardBase,
        IComponentBoardHasHandleReader,
        IComponentBoardHasGlobalReader
    {
        private (byte[] data, byte h) column;

        public SparseSetComponentBoard(int size, RevolutionWorld world) : base(size, world)
        {
            CurrentSize.Subscribe((_, next) => { Array.Resize(ref column.data, next * ComponentByteSize); }, true);
        }

        public Span<byte> Read()
        {
            return column.data;
        }

        public Span<T> Read<T>()
        {
            return MemoryMarshal.CreateSpan(
                ref Unsafe.As<byte, T>(ref MemoryMarshal.GetReference<byte>(column.data)),
                column.data.Length / ComponentByteSize
            );
        }

        public Span<byte> Read(in UComponentHandle handle)
        {
            return column.data.AsSpan(handle.Id * ComponentByteSize, ComponentByteSize);
        }

        public Span<T> Read<T>(in UComponentHandle handle)
        {
            return MemoryMarshal.CreateSpan(
                ref Unsafe.As<byte, T>(ref MemoryMarshal.GetReference<byte>(column.data)),
                column.data.Length / ComponentByteSize
            ).Slice(handle.Id, 1);
        }

        public override void Dispose()
        {
        }

        public override void CreateComponent(Span<UComponentHandle> output, Span<byte> data, bool singleData)
        {
            base.CreateComponent(output, data, singleData);
            if (data.Length == 0)
                return;

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

        public override bool Support<T>()
        {
            return base.Support<T>() && !ManagedTypeData<T>.ContainsReference;
        }
    }
}