using System.Runtime.CompilerServices;

namespace revecs.Core
{
    public struct UEntitySafe
    {
        public int Row;
        public int Version;

        public UEntityHandle Handle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(Row);
        }

        public UEntitySafe(int row, int version)
        {
            Row = row;
            Version = version;
        }

        public bool Equals(UEntitySafe other)
        {
            return Row == other.Row && Version == other.Version;
        }

        public override string ToString()
        {
            return $"EntitySafe({Row}; {Version})";
        }
    }
}