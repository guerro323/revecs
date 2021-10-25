using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;

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
            if (board is not IComponentBoardHasGlobalReader globalReader)
                throw new InvalidOperationException(
                    $"{ComponentTypeBoard.Names[type.Handle]} doesn't support global span"
                );

            return new SparseSetAccessor<T>(
                GetEntityComponentLink(type),
                globalReader.Read<T>()
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentSetAccessor<T> AccessComponentSet<T>(ComponentType<T> type)
        {
            var board = ComponentTypeBoard.Boards[type.Handle];
            if (board is not IComponentBoardHasHandleReader handleReader)
                throw new InvalidOperationException(
                    $"{ComponentTypeBoard.Names[type.Handle]} doesn't support handle reader"
                );

            return new ComponentSetAccessor<T>(
                GetEntityComponentLink(type),
                handleReader
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EntityComponentAccessor<T> AccessEntityComponent<T>(ComponentType<T> type)
        {
            var board = ComponentTypeBoard.Boards[type.Handle];
            if (board is not EntityComponentBoardBase entityBoard)
                throw new InvalidOperationException(
                    $"{ComponentTypeBoard.Names[type.Handle]} is not an EntityComponentBoard"
                );

            return new EntityComponentAccessor<T>(
                entityBoard
            );
        }
    }
}