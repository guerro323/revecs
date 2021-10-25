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
    }
}