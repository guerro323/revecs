using System.Runtime.CompilerServices;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Bases;
using revghost.Shared;

namespace revecs.Core
{
    public static class GameWorldLowLevel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentBoardBase GetComponentBoard(ComponentTypeBoard componentTypeBoard,
            ComponentType componentType)
        {
            return componentTypeBoard.Boards[componentType.Handle];
        }

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static UArchetypeHandle UpdateArchetype(
            ArchetypeBoard archetypeBoard,
            ComponentTypeBoard componentTypeBoard,
            EntityBoard entityBoard,
            EntityHasComponentBoard hasComponentBoard,
            UEntityHandle entityHandle)
        {
            var typeSpan = componentTypeBoard.All;
            var foundIndex = 0;

            using var disposable = DisposableArray<ComponentType>.Rent(typeSpan.Length, out var founds);

            for (var i = 0; i != typeSpan.Length; i++)
            {
                var metadataSpan = hasComponentBoard.GetColumn(typeSpan[i]);
                if (metadataSpan[entityHandle.Id])
                    founds[foundIndex++] = typeSpan[i];
            }

            if (foundIndex > 150)
                throw new InvalidOperationException("What are you trying to do with " + foundIndex + " components?");

            var archetype = archetypeBoard.GetOrCreateArchetype(founds.AsSpan(0, foundIndex));

            ref var currentArchetype = ref entityBoard.Archetypes[entityHandle.Id];
            if (currentArchetype.Equals(archetype) == false)
            {
                if (currentArchetype.Id > 0)
                    archetypeBoard.RemoveEntity(currentArchetype, entityHandle);
                archetypeBoard.AddEntity(archetype, entityHandle);

                currentArchetype = archetype;
            }

            return archetype;
        }
    }
}