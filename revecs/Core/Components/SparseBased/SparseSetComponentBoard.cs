using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;
using revecs.Utility;
using revghost.Shared;

namespace revecs.Core.Components.Boards
{
    public class SparseSetComponentBoard : LinkedComponentBoardBase
    {
        public byte[] ComponentDataColumn;

        public SparseSetComponentBoard(int size, RevolutionWorld world) : base(size, world)
        {
            CurrentSize.Subscribe((_, next) => { Array.Resize(ref ComponentDataColumn, next * ComponentByteSize); },
                true);
        }

        public override void AddComponent(UEntityHandle handle, Span<byte> data)
        {
            ref var component = ref BaseAddComponent(handle);
            data.CopyTo(
                ComponentDataColumn.AsSpan(component.Id * ComponentByteSize, ComponentByteSize)
            );
        }

        public override void RemoveComponent(UEntityHandle handle)
        {
            BaseRemoveComponent(handle);
        }

        public override Span<byte> GetComponentData(UEntityHandle handle)
        {
            return ComponentDataColumn.AsSpan(handle.Id * ComponentByteSize, ComponentByteSize);
        }

        public override Span<T> GetComponentData<T>(UEntityHandle handle)
        {
            return ComponentDataColumn
                .AsSpan()
                .UnsafeCast<byte, T>().Slice(handle.Id, 1);
        }

        public override void Dispose()
        {

        }
    }
}