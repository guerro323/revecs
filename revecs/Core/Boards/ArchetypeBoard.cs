using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Collections.Pooled;
using revghost.Shared.Collections;
using revghost.Shared.Events;
using revghost.Shared.Threading;

namespace revecs.Core.Boards
{
    public class ArchetypeBoard : BoardBase
    {
        private const int FastSearchLimit = 128;

        private readonly PooledList<UArchetypeHandle>[] _fastSearch;

        private IntRowCollection _rows;

        private (int[] sum, 
            ComponentType[][] componentTypes,
            List<UEntityHandle>[] entity,
            BusySynchronizationManager[] sync) column;

        private Bindable<UArchetypeHandle> _handleUpdateBindable;
        public readonly ReadOnlyBindable<UArchetypeHandle> HandleUpdate;

        public ArchetypeBoard(RevolutionWorld world) : base(world)
        {
            _rows = new IntRowCollection(onResize);
            _fastSearch = new PooledList<UArchetypeHandle>[FastSearchLimit];

            HandleUpdate = _handleUpdateBindable = new Bindable<UArchetypeHandle>();

            for (var i = 0; i < FastSearchLimit; i++)
                _fastSearch[i] = new PooledList<UArchetypeHandle>();
            
            onResize(0, 0);
        }

        private void onResize(int prev, int next)
        {
            Array.Resize(ref column.entity, next);
            Array.Resize(ref column.sum, next);
            Array.Resize(ref column.componentTypes, next);
            Array.Resize(ref column.sync, next);
        }

        public override void Dispose()
        {
            foreach (var list in _fastSearch)
                list.Dispose();
        }
        
        private readonly BusySynchronizationManager _createArchetypeSync = new();

        // insanely fast if componentTypes is under FastSearchLimit count
        public UArchetypeHandle GetOrCreateArchetype(Span<ComponentType> componentTypes)
        {
            var sum = 0;
            for (var i = 0; i < componentTypes.Length; i++) sum += componentTypes[i].Handle;

            // search only on existing archetypes that have the same component count
            // this save some of the perf cost  
            
            // obtain a stable pointer to OrderedActiveRows
            if (componentTypes.Length > FastSearchLimit)
                _createArchetypeSync.Lock();
            
            var spanToSearch = componentTypes.Length > FastSearchLimit
                ? _rows.OrderedActiveRows
                : MemoryMarshal.Cast<UArchetypeHandle, int>(_fastSearch[componentTypes.Length].Span);
            
            if (componentTypes.Length > FastSearchLimit)
                _createArchetypeSync.Unlock();

            var length = spanToSearch.Length;

            // this is done to avoid bound checking (we save like 5% of the cost)
            _createArchetypeSync.Lock(); // obtain a stable pointer for sum and componentTypes
            ref var columnSumPtr = ref MemoryMarshal.GetArrayDataReference(column.sum);
            ref var columnTypePtr = ref MemoryMarshal.GetArrayDataReference(column.componentTypes);
            _createArchetypeSync.Unlock();
            
            for (var i = 0; i < length; i++)
            {
                var arch = spanToSearch[i];
                if (Unsafe.Add(ref columnSumPtr, arch) != sum)
                    continue;

                if (Unsafe.Add(ref columnTypePtr, arch)
                    .AsSpan()
                    .SequenceEqual(componentTypes))
                    return new UArchetypeHandle(arch);
            }

            return new UArchetypeHandle(createArchetype(componentTypes, sum));
        }

        private int createArchetype(ReadOnlySpan<ComponentType> componentTypes, int sum)
        {
            using var sync = _createArchetypeSync.Synchronize();
            
            var row = _rows.CreateRow();
            _rows.GetColumn(row, ref column.entity) = new List<UEntityHandle>();
            _rows.GetColumn(row, ref column.sum) = sum;
            _rows.GetColumn(row, ref column.componentTypes) = componentTypes.ToArray();
            _rows.GetColumn(row, ref column.sync) = new BusySynchronizationManager();

            _handleUpdateBindable.Value = new UArchetypeHandle(row);

            if (componentTypes.Length < FastSearchLimit)
            {
                _fastSearch[componentTypes.Length].Add(new UArchetypeHandle(row));
            }

            return row;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEntity(UArchetypeHandle row, UEntityHandle entity)
        {
            // thread-safe
            // using var sync = column.sync[row.Id].Synchronize();
            
            var list = column.entity[row.Id];
            list.Add(entity);

            /*ref var listRef = ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(list));
            var count = list.Count;

            var lastEntity = default(UEntityHandle);
            var nearest = count;

            // we do it the reverse way since there are much more cases where we will find the nearest entity from
            // the end than the start
            // this save almost nothing in term of cost if by default it's not ordered (perhaps 5% to 10%)
            // in an ideal world, consecutive AddEntity calls would always be entity+1 (which save 95% of the cost)
            while (count-- > 0)
            {
                var ent = Unsafe.Add(ref listRef, count);
                if (ent.Id > lastEntity.Id && ent.Id < entity.Id)
                {
                    lastEntity = ent;
                    nearest = count + 1;
                }
            }

            list.Insert(nearest, entity);*/
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEntity(UArchetypeHandle row, UEntityHandle entity)
        {
            // thread-safe
            using var sync = column.sync[row.Id].Synchronize();
            
            column.entity[row.Id].Remove(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<ComponentType> GetComponentTypes(UArchetypeHandle row)
        {
            return column.componentTypes[row.Id];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<UEntityHandle> GetEntities(UArchetypeHandle row)
        {
#if DEBUG
            if (row.Id >= column.entity.Length)
                throw new IndexOutOfRangeException($"{row} > {column.entity.Length}");
#endif
            
            return CollectionsMarshal.AsSpan(column.entity[row.Id]);
        }
    }
}