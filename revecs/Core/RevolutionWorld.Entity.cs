using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace revecs.Core
{
    public partial class RevolutionWorld
    {
        [Conditional("DEBUG")]
        public void ThrowOnInvalidHandle(UEntityHandle handle)
        {
            if (handle.Id == 0)
                throw new InvalidOperationException("You've passed an invalid handle");
            if (EntityBoard.Exists[handle.Id] == false)
                throw new InvalidOperationException(
                    $"The RevolutionWorld does not contains a handle with id '{handle.Id}'");
        }

        /// <summary>
        /// Create an entity in the world
        /// </summary>
        /// <remarks>
        /// The entity may be recycled.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UEntityHandle CreateEntity()
        {
            Unsafe.SkipInit(out UEntityHandle handle);
            EntityBoard.CreateEntities(MemoryMarshal.CreateSpan(ref handle, 1));
            ArchetypeUpdateBoard.Queue(handle);

            return handle;
        }

        /// <summary>
        /// Create a batch of entities in the world
        /// </summary>
        /// <param name="entities">The entities output</param>
        /// <remarks>
        /// The entities may be recycled.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateEntities(Span<UEntityHandle> entities)
        {
            EntityBoard.CreateEntities(entities);
            foreach (var handle in entities)
                ArchetypeUpdateBoard.Queue(handle);
        }

        /// <summary>
        /// Destroy an entity in this world
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyEntity(UEntityHandle handle)
        {
            DestroyEntities(MemoryMarshal.CreateSpan(ref handle, 1));
        }

        /// <summary>
        /// Destroy a batch of entities in this world
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyEntities(Span<UEntityHandle> entities)
        {
            foreach (ref readonly var entity in entities)
            {
                foreach (ref readonly var compType in ComponentTypeBoard.All)
                {
                    if (!EntityHasComponentBoard.GetColumn(compType)[entity.Id])
                        continue;

                    RemoveComponent(entity, compType);
                }
                
                ArchetypeUpdateBoard.Queue(entity);
                ArchetypeUpdateBoard.Update(entity);
            }

            EntityBoard.DestroyEntities(entities);
        }

        /// <summary>
        /// Check whether or not an entity exist
        /// </summary>
        /// <param name="handle">The raw handle of the entity</param>
        /// <returns>Existence of the handle</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Exists(in UEntityHandle handle)
        {
            return EntityBoard.Exists[handle.Id];
        }

        /// <summary>
        /// Check whether or not an entity with version exist
        /// </summary>
        /// <param name="safe">The safe handle of the entity</param>
        /// <returns>Existence of the safe handle</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Exists(in UEntitySafe safe)
        {
            return EntityBoard.Exists[safe.Row] && EntityBoard.Versions[safe.Row] == safe.Version;
        }

        /// <summary>
        ///     Get a safe version of the handle (with version)
        /// </summary>
        /// <param name="handle">The handle to transform</param>
        /// <returns></returns>
        /// <remarks>
        ///     It may also be possible that you have an invalid handle, and that you want to check for an updated one.
        ///     For example: oldEntity.Version != gameWorld.Safe(oldEntity.Handle).Version
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UEntitySafe Safe(in UEntityHandle handle)
        {
            return new UEntitySafe(handle.Id, EntityBoard.Versions[handle.Id]);
        }
    }
}