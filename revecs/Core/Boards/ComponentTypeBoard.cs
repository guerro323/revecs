using System.Runtime.InteropServices;
using revecs.Core.Components.Boards.Bases;
using revecs.Core.Components.Boards.Modifiers;
using revecs.Utility;

namespace revecs.Core.Boards
{
    public class ComponentTypeBoard : BoardBase
    {
        private readonly Bindable<int> _currentSizeBindable;
        public readonly ReadOnlyBindable<int> CurrentSize;
        private IntRowCollection _rows;

        private (string[] name, int[] size, ComponentBoardBase[] board) column;

        public ComponentTypeBoard(RevolutionWorld world) : base(world)
        {
            CurrentSize = new ReadOnlyBindable<int>(_currentSizeBindable = new Bindable<int>());

            _rows = new IntRowCollection((_, next) =>
            {
                Array.Resize(ref column.name, next);
                Array.Resize(ref column.size, next);
                Array.Resize(ref column.board, next);

                _currentSizeBindable!.Value = next;
            });
            _rows.OnResize!(0, 0);
        }

        public Span<ComponentType> All => MemoryMarshal.Cast<int, ComponentType>(_rows.OrderedActiveRows);

        public ReadOnlySpan<string> Names => column.name;
        public ReadOnlySpan<int> Sizes => column.size;
        public ReadOnlySpan<ComponentBoardBase> Boards => column.board;

        public override void Dispose()
        {
            for (var i = 0; i < _rows.MaxId; i++)
            {
                var board = column.board[i];

                try
                {
                    board.Dispose();
                }
                catch (Exception)
                {
                    Console.WriteLine($"Error when disposing component type {column.name[i]}");
                    throw;
                }
            }

            column.name.AsSpan().Clear();
            column.board.AsSpan().Clear();

            _currentSizeBindable.Dispose();
        }

        public ComponentType CreateComponentType(string name, ComponentBoardBase board)
        {
            var row = _rows.CreateRow();
            column.name[row] = name;
            column.board[row] = board;

            if (board is IComponentBoardHasSize hasSize)
                column.size[row] = hasSize.ComponentByteSize;
            else
                column.size[row] = -1; // unknown

            return new ComponentType(row);
        }
    }
}