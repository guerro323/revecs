using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Components.Boards.Bases;
using revghost.Shared;

namespace revecs.Core
{
    public partial class RevolutionWorld
    {
        /// <summary>
        ///     Add a component to an entity
        /// </summary>
        /// <param name="entityHandle"></param>
        /// <param name="componentType"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponent(UEntityHandle entityHandle, ComponentType componentType,
            Span<byte> data = default)
        {
            ThrowOnInvalidHandle(entityHandle);

            var componentBoard = GameWorldLowLevel.GetComponentBoard(ComponentTypeBoard, componentType);
            Unsafe.As<ComponentBoardBase>(componentBoard)
                .AddComponent(entityHandle, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponent<T>(UEntityHandle entityHandle, ComponentType componentType,
            Span<T> data = default)
        {
            BoardSupportTypeThrow<T>(componentType);

            AddComponent(entityHandle, componentType, MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(data)),
                Unsafe.SizeOf<T>() * data.Length
            ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponent<T>(UEntityHandle entityHandle, ComponentType componentType,
            in T data = default)
            => AddComponent(entityHandle, componentType, MemoryMarshal.CreateSpan(ref Unsafe.AsRef(data), 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponent<T>(UEntityHandle entityHandle, ComponentType<T> componentType,
            Span<T> data = default)
        {
            AddComponent(entityHandle, componentType, MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(data)),
                Unsafe.SizeOf<T>() * data.Length
            ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponent<T>(UEntityHandle entityHandle, ComponentType<T> componentType,
            in T data = default)
            => AddComponent(entityHandle, componentType, MemoryMarshal.CreateSpan(ref Unsafe.AsRef(data), 1));
    }
}