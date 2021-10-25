namespace revecs.Core
{
    public readonly struct UArchetypeHandle : IEquatable<UArchetypeHandle>
    {
        public readonly int Id;

        public UArchetypeHandle(int id)
        {
            Id = id;
        }

        public bool Equals(UArchetypeHandle other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return obj is UArchetypeHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public override string ToString()
        {
            return $"Archetype({Id})";
        }
    }
}