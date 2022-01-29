using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Components.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;
using revecs.Utility;

namespace revecs.Core
{
    public partial class RevolutionWorld
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetComponentData<T>(UEntityHandle handle, ComponentType type)
        {
            return ref MemoryMarshal.GetReference(ReadComponent<T>(handle, type));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetComponentData<T>(UEntityHandle handle, ComponentType<T> type)
        {
            return ref MemoryMarshal.GetReference(ReadComponent(handle, type));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SparseSetAccessor<T> AccessSparseSet<T>(ComponentType<T> type)
        {
            var board = ComponentTypeBoard.Boards[type.Handle];
            if (board is SparseSetComponentBoard sparseBoard)
            {
                return new SparseSetAccessor<T>(
                    sparseBoard.EntityLink,
                    sparseBoard.ComponentDataColumn.AsSpan().UnsafeCast<byte, T>()
                );
            }

            if (board is SparseSetManagedComponentBoard<T> managedBoard)
            {
                return new SparseSetAccessor<T>(
                    managedBoard.EntityLink,
                    managedBoard.ComponentDataColumn.AsSpan()
                );
            }

            throw new InvalidOperationException("invalid board found");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityComponentAccessor<T> AccessEntityComponent<T>(ComponentType<T> type)
        {
            var board = ComponentTypeBoard.Boards[type.Handle];
            return new EntityComponentAccessor<T>(
                board
            );
        }
    }
}