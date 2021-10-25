using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revghost.Shared.Collections;
using revghost.Shared.Events;

namespace revecs.Core.Boards
{
    public class EntityBoard : BoardBase
    {
        private readonly Bindable<int> _currentSizeBindable = new();

        private IntRowCollection _rows;

        private (int[] archetype, int[] version) column;

        public EntityBoard(RevolutionWorld world) : base(world)
        {
            _rows = new IntRowCollection((_, next) =>
            {
                Array.Resize(ref column.archetype, next);
                Array.Resize(ref column.version, next);

                _currentSizeBindable.Value = next;
            });
            _rows.OnResize!(0, 0);
        }

        public ReadOnlyBindable<int> CurrentSize => _currentSizeBindable;

        public Span<UArchetypeHandle> Archetypes => MemoryMarshal.Cast<int, UArchetypeHandle>(column.archetype);
        public Span<int> Versions => column.version;
        public Span<bool> Exists => _rows.RowStates;

        public override void Dispose()
        {
            _currentSizeBindable.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateEntities(Span<UEntityHandle> output)
        {
            _rows.CreateRowBulk(MemoryMarshal.Cast<UEntityHandle, int>(output));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyEntities(Span<UEntityHandle> input)
        {
            _rows.TrySetUnusedRowBulk(MemoryMarshal.Cast<UEntityHandle, int>(input));

            foreach (var row in input) column.version[row.Id]++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<UEntityHandle> GetEntities()
        {
            return MemoryMarshal.Cast<int, UEntityHandle>(_rows.OrderedActiveRows);
        }
    }
}