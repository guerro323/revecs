using System.Runtime.CompilerServices;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;

namespace revecs.Core
{
    public partial class RevolutionWorld
    {
        private void BoardSupportTypeThrow<T>(ComponentType type, ComponentBoardBase board)
        {
            if (board is IComponentBoardHasTypeSupport typeSupport
                && !typeSupport.Support<T>())
            {
                var name = ComponentTypeBoard.Names[type.Handle];

                throw new InvalidOperationException(
                    $"Board '{board.GetType()}' of component type '{name}' doesn't support managed type '{typeof(T)}'"
                );
            }
        }
        
        private void BoardSupportTypeThrow<T>(ComponentType type)
        {
            BoardSupportTypeThrow<T>(type, ComponentTypeBoard.Boards[type.Handle]);
        }
        
        public bool HasComponent(UEntityHandle handle, ComponentType type)
        {
            if (!Exists(handle))
                return false;

            return EntityHasComponentBoard.GetColumn(type)[handle.Id];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> ReadComponent(UEntityHandle handle, ComponentType type)
        {
            ThrowOnInvalidHandle(handle);

            var board = ComponentTypeBoard.Boards[type.Handle];
            return Unsafe.As<ComponentBoardBase>(board).GetComponentData(handle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> ReadComponent<T>(UEntityHandle handle, ComponentType type)
        {
            ThrowOnInvalidHandle(handle);

            var board = ComponentTypeBoard.Boards[type.Handle];

            BoardSupportTypeThrow<T>(type, board);
            
            return Unsafe.As<ComponentBoardBase>(board).GetComponentData<T>(handle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> ReadComponent<T>(UEntityHandle handle, ComponentType<T> type)
        {
            ThrowOnInvalidHandle(handle);

            var board = ComponentTypeBoard.Boards[type.Handle];
            // We assume that the user took the type arg from GetComponentType(...)
            // so there should be no need to check IComponentBoardHasTypeSupport.Support<T>()
            return Unsafe.As<ComponentBoardBase>(board).GetComponentData<T>(handle);
        }
    }
}