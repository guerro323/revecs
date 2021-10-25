using System.Runtime.CompilerServices;
using revghost.Shared.Events;

namespace revecs.Core.Boards
{
    public struct EntityComponentLink
    {
        /// <summary>
        ///     The assigned component meta
        /// </summary>
        public readonly int Assigned;

        /// <summary>
        ///     Is the assignment null?
        /// </summary>
        public bool Null => Assigned == 0;

        /// <summary>
        ///     Is the assignment valid?
        /// </summary>
        public bool Valid => Assigned != 0;

        /// <summary>
        ///     Is this a custom component?
        /// </summary>
        public bool IsShared => Assigned < 0;

        public bool IsReference => Assigned > 0;

        /// <summary>
        ///     The reference to the non custom component
        /// </summary>
        public UComponentHandle Handle => IsReference ? new UComponentHandle(Assigned) : default;

        /// <summary>
        ///     The reference to the entity that share the component
        /// </summary>
        public UEntityHandle Entity => IsShared ? new UEntityHandle(-Assigned) : default;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityComponentLink Reference(UComponentHandle handle)
        {
            return new(handle.Id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityComponentLink Shared(UEntityHandle handle)
        {
            return new(-handle.Id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EntityComponentLink(int assigned)
        {
            Assigned = assigned;
        }
    }

    public class EntityComponentLinkBoard : BoardBase
    {
        private EntityComponentLink[][] _componentColumnPerType;
        private readonly BindableListener _componentTypeResizeListener;

        private readonly BindableListener _entityResizeListener;

        private int _lastEntitySize;

        public EntityComponentLinkBoard(RevolutionWorld world) : base(world)
        {
            _componentColumnPerType = Array.Empty<EntityComponentLink[]>();

            var entityBoard = world.GetBoard<EntityBoard>("Entity");
            var componentTypeBoard = world.GetBoard<ComponentTypeBoard>("ComponentType");

            _entityResizeListener = entityBoard.CurrentSize.Subscribe(EntityOnResize, true);
            _componentTypeResizeListener = componentTypeBoard.CurrentSize.Subscribe(ComponentTypeOnResize, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<EntityComponentLink> GetColumn(ComponentType type)
        {
            return _componentColumnPerType[type.Handle];
        }

        private void EntityOnResize(int prev, int curr)
        {
            foreach (ref var column in _componentColumnPerType.AsSpan()) Array.Resize(ref column, curr);

            _lastEntitySize = curr;
        }

        private void ComponentTypeOnResize(int _, int curr)
        {
            var previousSize = _componentColumnPerType.Length;

            Array.Resize(ref _componentColumnPerType, curr);

            foreach (ref var column in _componentColumnPerType.AsSpan(previousSize))
                column = new EntityComponentLink[_lastEntitySize];
        }

        public override void Dispose()
        {
            _entityResizeListener.Dispose();
            _componentTypeResizeListener.Dispose();
        }

        /// <summary>
        ///     Assign a linked component to an entity.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="componentReference"></param>
        /// <returns>The previous component id</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UComponentHandle AssignComponentReference(UEntityHandle entity, UComponentReference componentReference)
        {
            ref var current = ref GetColumn(componentReference.Type)[entity.Id];
            var previous = current;

            current = EntityComponentLink.Reference(componentReference.Handle);
            return previous.Handle;
        }
    }
}