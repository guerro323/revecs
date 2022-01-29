using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Modifiers;
using revecs.Utility;
using revghost.Shared.Collections;
using revghost.Shared.Events;

namespace revecs.Core.Components.Boards.Bases
{
    public abstract class LinkedComponentBoardBase : ComponentBoardBase, IComponentBoardHasSize,
        IComponentBoardHasTypeSupport
    {
        public record struct UComponentHandle(int Id);

        private readonly Bindable<int> _currentSizeBindable;
        public readonly ReadOnlyBindable<int> CurrentSize;

        private IntRowCollection _rows;
        private UComponentHandle[] entityLink;

        public LinkedComponentBoardBase(int size, RevolutionWorld world) : base(world)
        {
            ComponentByteSize = size;

            CurrentSize = new ReadOnlyBindable<int>(_currentSizeBindable = new Bindable<int>());

            _rows = new IntRowCollection((_, next) => { _currentSizeBindable.Value = next; });

            _rows.OnResize!(0, 0);

            world.EntityBoard.CurrentSize.Subscribe((prev, next) => { Array.Resize(ref entityLink, next); }, true);
        }

        public Span<UComponentHandle> EntityLink => entityLink;

        public int ComponentByteSize { get; }

        public virtual bool Support<T>()
        {
            return ManagedTypeData<T>.Size == ComponentByteSize;
        }

        public virtual UComponentHandle CreateComponent()
        {
            UComponentHandle output = default;
            _rows.CreateRowBulk(MemoryMarshal.Cast<UComponentHandle, int>(output.ToSpan()));
            return output;
        }

        public virtual void DestroyComponent(UComponentHandle handle)
        {
            _rows.TrySetUnusedRow(handle.Id);
        }

        protected ref UComponentHandle BaseAddComponent(UEntityHandle handle)
        {
            ref var component = ref EntityLink[handle.Id];
            if (component.Id == 0)
            {
                component = CreateComponent();

                World.ArchetypeUpdateBoard.Queue(handle);
                
                HasComponentBoard.EntityHasComponentColumn[ComponentType.Handle][handle.Id] = true;
            }

            EntityLink[handle.Id] = component;
            return ref component;
        }

        protected UComponentHandle BaseRemoveComponent(UEntityHandle handle)
        {
            ref var index = ref EntityLink[handle.Id];
            if (index.Id != 0)
            {
                DestroyComponent(index);
                World.ArchetypeUpdateBoard.Queue(handle);
                
                HasComponentBoard.EntityHasComponentColumn[ComponentByteSize][handle.Id] = false;

                var cpy = index;
                index = default;

                return cpy;
            }

            return default;
        }
    }
}