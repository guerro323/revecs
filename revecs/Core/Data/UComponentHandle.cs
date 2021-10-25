namespace revecs.Core
{
    public readonly struct UComponentHandle : IEquatable<UComponentHandle>
    {
        public readonly int Id;

        public UComponentHandle(int id)
        {
            Id = id;
        }

        public bool Equals(UComponentHandle other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return obj is UComponentHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public override string ToString()
        {
            return $"{Id}";
        }
    }
}