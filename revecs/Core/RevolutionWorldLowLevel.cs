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
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveComponentReference(LinkedComponentBoardBase componentBoard, ComponentType componentType,
            EntityComponentLinkBoard entityBoard, UEntityHandle entityHandle)
        {
            // assigning a reference to 0 will indicate that the entity doesn't have this component anymore
            var fakeReference = new UComponentReference(componentType, default);
            
            var previousComponent = entityBoard.AssignComponentReference(entityHandle, fakeReference);
            if (previousComponent.Id > 0)
            {
                var refs = componentBoard.RemoveReference(previousComponent, entityHandle);

                // nobody reference this component anymore, let's remove the row
                if (refs == 0)
                    componentBoard.DestroyComponent(previousComponent);

                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AssignComponent(LinkedComponentBoardBase componentBoard,
            UComponentReference componentReference,
            EntityComponentLinkBoard entityBoard, UEntityHandle entity)
        {
            componentBoard.AddReference(componentReference.Handle, entity);

            var previousComponent = entityBoard.AssignComponentReference(entity, componentReference);
            if (previousComponent.Id > 0)
            {
                var refs = componentBoard.RemoveReference(previousComponent, entity);

                // nobody reference this component anymore, let's remove the row
                if (refs == 0)
                    componentBoard.DestroyComponent(previousComponent);

                return false;
            }

            return true;
        }

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static UArchetypeHandle UpdateArchetype(
            ArchetypeBoard archetypeBoard,
            ComponentTypeBoard componentTypeBoard,
            EntityBoard entityBoard,
            EntityComponentLinkBoard componentLinkBoard,
            UEntityHandle entityHandle)
        {
            var typeSpan = componentTypeBoard.All;
            var foundIndex = 0;

            using var disposable = DisposableArray<ComponentType>.Rent(typeSpan.Length, out var founds);

            for (var i = 0; i != typeSpan.Length; i++)
            {
                var metadataSpan = componentLinkBoard.GetColumn(typeSpan[i]);
                if (metadataSpan[entityHandle.Id].Valid)
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