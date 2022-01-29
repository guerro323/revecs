using revecs.Core.Boards;
using revtask.Core;
using revtask.OpportunistJobRunner;

namespace revecs.Core
{
    public partial class RevolutionWorld : IDisposable
    {
        public BoardMap BoardMap;
        
        public readonly ArchetypeBoard ArchetypeBoard;
        public readonly ArchetypeUpdateBoard ArchetypeUpdateBoard;
        public readonly ComponentTypeBoard ComponentTypeBoard;
        public readonly EntityBoard EntityBoard;
        public readonly EntityHasComponentBoard EntityHasComponentBoard;
        
        public RevolutionWorld()
        {
            BoardMap = BoardMap.Create();
            
            AddBoard("Entity", EntityBoard = new EntityBoard(this));
            AddBoard("ComponentType", ComponentTypeBoard = new ComponentTypeBoard(this));
            AddBoard("EntityHasComponentBoard", EntityHasComponentBoard = new EntityHasComponentBoard(this));
            AddBoard("Archetype", ArchetypeBoard = new ArchetypeBoard(this));
            AddBoard("ArchetypeUpdate", ArchetypeUpdateBoard = new ArchetypeUpdateBoard(this));
        }

        public void Dispose()
        {
        }

        public void AddBoard(string name, BoardBase board)
        {
            BoardMap.Add(name, board);
        }

        public BoardBase? GetBoardOrDefault(string name)
        {
            return BoardMap.Get(name);
        }

        public TBoard? GetBoardOrDefault<TBoard>(string name)
            where TBoard : BoardBase
        {
            return (TBoard?) GetBoardOrDefault(name);
        }

        public TBoard GetBoard<TBoard>(string name)
            where TBoard : BoardBase
        {
            var board = GetBoardOrDefault(name);
            if (board == null)
                throw new NullReferenceException(nameof(TBoard) + " for " + name);
            return (TBoard) board;
        }
    }

    public struct BoardMap : IDisposable
    {
        private Dictionary<string, BoardBase> map;

        public void Add(string name, BoardBase board)
        {
            map[name] = board;
        }

        public static BoardMap Create()
        {
            return new BoardMap
            {
                map = new Dictionary<string, BoardBase>()
            };
        }

        public BoardBase? Get(string name)
        {
            map.TryGetValue(name, out var value);
            return value;
        }

        public void Dispose()
        {
            foreach (var (_, obj) in map)
                if (obj is IDisposable disposable)
                    disposable.Dispose();

            map.Clear();
        }
    }
}