using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;
using revecs.Utility;

namespace revecs.Core.Components.Boards
{
    public class SparseSetManagedComponentBoard<T> : LinkedComponentBoardBase
    {
        public T[] ComponentDataColumn;

        public SparseSetManagedComponentBoard(int size, RevolutionWorld world) : base(size, world)
        {
            CurrentSize.Subscribe((_, next) => { Array.Resize(ref ComponentDataColumn, next * ComponentByteSize); },
                true);
        }

        public override void Dispose()
        {
        }

        public override void AddComponent(UEntityHandle handle, Span<byte> data)
        {
            ref readonly var component = ref BaseAddComponent(handle);
            var span = data.UnsafeCast<byte, T>();
            if (span.Length > 0)
                ComponentDataColumn[component.Id] = span[0];
        }

        public override void RemoveComponent(UEntityHandle handle)
        {
            var component = BaseRemoveComponent(handle);
            ComponentDataColumn[component.Id] = default!;
        }

        public override Span<byte> GetComponentData(UEntityHandle handle)
        {
            return ComponentDataColumn.AsSpan(handle.Id, 1).UnsafeCast<T, byte>();
        }

        public override Span<TTo> GetComponentData<TTo>(UEntityHandle handle)
        {
            return ComponentDataColumn.AsSpan(handle.Id, 1).UnsafeCast<T, TTo>();
        }
    }
}