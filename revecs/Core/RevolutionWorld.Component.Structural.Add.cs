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
        public UComponentReference AddComponent(UEntityHandle entityHandle, ComponentType componentType,
            Span<byte> data = default)
        {
            ThrowOnInvalidHandle(entityHandle);

            Unsafe.SkipInit(out UComponentReference output);

            var componentBoard = GameWorldLowLevel.GetComponentBoard(ComponentTypeBoard, componentType);
            if (componentBoard.CustomEntityOperation)
            {
                Unsafe.As<EntityComponentBoardBase>(componentBoard)
                    .AddComponent(
                        MemoryMarshal.CreateSpan(ref entityHandle, 1),
                        MemoryMarshal.CreateSpan(ref output, 1),
                        data, true
                    );

                return output;
            }

            // it is assumed that for now a board is either:
            // - EntityComponentBoardBase
            // - LinkedComponentBoardBase
            // so we can 'safely' cast into a linked board here.
            var linkedBoard = Unsafe.As<ComponentBoardBase, LinkedComponentBoardBase>(ref componentBoard);

            Unsafe.SkipInit(out UComponentHandle handle);
            linkedBoard.CreateComponent(MemoryMarshal.CreateSpan(ref handle, 1), data, true);
            output = new UComponentReference(componentType, handle);

            linkedBoard.SetOwner(output.Handle, entityHandle);
            // Only update archetype if this is a new component to the entity, and not just an update
            if (GameWorldLowLevel.AssignComponent(linkedBoard, output, EntityComponentLinkBoard, entityHandle))
                ArchetypeUpdateBoard.Queue(entityHandle);

            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UComponentReference AddComponent<T>(UEntityHandle entityHandle, ComponentType componentType,
            Span<T> data = default)
        {
            BoardSupportTypeThrow<T>(componentType);

            return AddComponent(entityHandle, componentType, MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(data)),
                Unsafe.SizeOf<T>() * data.Length
            ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UComponentReference AddComponent<T>(UEntityHandle entityHandle, ComponentType componentType,
            in T data = default)
            => AddComponent(entityHandle, componentType, MemoryMarshal.CreateSpan(ref Unsafe.AsRef(data), 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UComponentReference AddComponent<T>(UEntityHandle entityHandle, ComponentType<T> componentType,
            Span<T> data = default)
        {
            return AddComponent(entityHandle, componentType, MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(data)),
                Unsafe.SizeOf<T>() * data.Length
            ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UComponentReference AddComponent<T>(UEntityHandle entityHandle, ComponentType<T> componentType,
            in T data = default)
            => AddComponent(entityHandle, componentType, MemoryMarshal.CreateSpan(ref Unsafe.AsRef(data), 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddComponentBatched(Span<UEntityHandle> input, Span<UComponentReference> output,
            ComponentType componentType,
            Span<byte> data = default, bool singleData = true)
        {
#if DEBUG
            foreach (var ent in input)
                ThrowOnInvalidHandle(ent);
#endif

            var componentBoard = GameWorldLowLevel.GetComponentBoard(ComponentTypeBoard, componentType);
            if (componentBoard.CustomEntityOperation)
            {
                Unsafe.As<EntityComponentBoardBase>(componentBoard)
                    .AddComponent(input, output, data, singleData);

                return;
            }

            // it is assumed that for now a board is either:
            // - EntityComponentBoardBase
            // - LinkedComponentBoardBase
            // so we can 'safely' cast into a linked board here.
            var linkedBoard = Unsafe.As<ComponentBoardBase, LinkedComponentBoardBase>(ref componentBoard);

            var count = input.Length;

            using var disposable = DisposableArray<UComponentHandle>.Rent(count, out var array);
            linkedBoard.CreateComponent(array.AsSpan(0, count), data, singleData);

            ref var outputRef = ref MemoryMarshal.GetReference(output);
            ref var arrayRef = ref MemoryMarshal.GetArrayDataReference(array);
            for (var i = 0; i < count; i++)
            {
                Unsafe.Add(ref outputRef, i) = new UComponentReference(componentType, Unsafe.Add(ref arrayRef, i));
            }

            linkedBoard.SetOwner(array, input);
            linkedBoard.FastAssignReference(output, input, EntityComponentLinkBoard);
        }
    }
}