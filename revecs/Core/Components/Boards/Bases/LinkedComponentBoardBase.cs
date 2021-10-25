using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Boards;
using revecs.Core.Components.Boards.Modifiers;
using revecs.Utility;

namespace revecs.Core.Components.Boards.Bases
{
    public abstract class LinkedComponentBoardBase : ComponentBoardBase, IComponentBoardHasSize,
        IComponentBoardHasTypeSupport
    {
        private readonly Bindable<int> _currentSizeBindable;
        public readonly ReadOnlyBindable<int> CurrentSize;

        private IntRowCollection _rows;
        private (UEntityHandle[] owner, List<UEntityHandle>[] references) column;

        public LinkedComponentBoardBase(int size, RevolutionWorld world) : base(world)
        {
            ComponentByteSize = size;

            CurrentSize = new ReadOnlyBindable<int>(_currentSizeBindable = new Bindable<int>());

            _rows = new IntRowCollection((prev, next) =>
            {
                Array.Resize(ref column.owner, next);
                Array.Resize(ref column.references, next);

                for (; prev < next; prev++)
                    column.references[prev] = new List<UEntityHandle>();

                _currentSizeBindable.Value = next;
            });
            _rows.OnResize!(0, 0);
        }

        public Span<UEntityHandle> Owners => column.owner;

        public int ComponentByteSize { get; }

        public virtual bool Support<T>()
        {
            return ManagedTypeData<T>.Size == ComponentByteSize;
        }

        public virtual void AddReference(in UComponentHandle row, in UEntityHandle entity)
        {
            ref var list = ref column.references[row.Id];
            list.Add(entity);
        }

        public virtual int RemoveReference(in UComponentHandle row, in UEntityHandle entity)
        {
            ref var list = ref column.references[row.Id];
            list.Remove(entity);
            return list.Count;
        }

        public virtual void SetOwner(in UComponentHandle row, in UEntityHandle entity)
        {
            column.owner[row.Id] = entity;
        }
        
        public virtual void SetOwner(in Span<UComponentHandle> row, in Span<UEntityHandle> entity)
        {
            var length = entity.Length;

            ref var rowRef = ref MemoryMarshal.GetReference(row);
            ref var entityRef = ref MemoryMarshal.GetReference(entity);

            ref var ownerRef = ref MemoryMarshal.GetArrayDataReference(column.owner);

            for (var i = 0; i < length; i++)
            {
                Unsafe.Add(ref ownerRef, Unsafe.Add(ref rowRef, i).Id) = Unsafe.Add(ref entityRef, i);
            }
        }

        public virtual void FastAssignReference(in Span<UComponentReference> row, in Span<UEntityHandle> entity,
            EntityComponentLinkBoard entityBoard)
        {
            var length = row.Length;
            
            ref var rowRef = ref MemoryMarshal.GetReference(row);
            ref var entityRef = ref MemoryMarshal.GetReference(entity);

            for (var i = 0; i < length; i++)
            {
                ref var componentReference = ref Unsafe.Add(ref rowRef, i);
                ref var handle = ref Unsafe.Add(ref entityRef, i);

                AddReference(componentReference.Handle, handle);

                var previousComponent = entityBoard.AssignComponentReference(handle, componentReference);
                if (previousComponent.Id > 0)
                {
                    var refs = RemoveReference(previousComponent, handle);

                    // nobody reference this component anymore, let's remove the row
                    if (refs == 0)
                        DestroyComponent(previousComponent);
                }
            }
        }

        public virtual void CreateComponent(Span<UComponentHandle> output, Span<byte> data, bool singleData)
        {
            _rows.CreateRowBulk(MemoryMarshal.Cast<UComponentHandle, int>(output));
        }

        public virtual void DestroyComponent(UComponentHandle handle)
        {
            _rows.TrySetUnusedRow(handle.Id);
        }
    }
}