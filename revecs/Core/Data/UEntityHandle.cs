namespace revecs.Core
{
    public readonly struct UEntityHandle : IEquatable<UEntityHandle>
    {
        public readonly int Id;

        public UEntityHandle(int id)
        {
            Id = id;
        }

        public override string ToString()
        {
            return $"Handle({Id})";
        }

        public bool Equals(UEntityHandle other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return obj is UEntityHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }
    }
}