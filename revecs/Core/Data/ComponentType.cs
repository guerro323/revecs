using System.Runtime.CompilerServices;

namespace revecs.Core
{
    public readonly struct ComponentType : IEquatable<ComponentType>
    {
        public readonly int Handle;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentType(int handle)
        {
            Handle = handle;
        }

        public bool Equals(ComponentType other)
        {
            return Handle == other.Handle;
        }

        public override bool Equals(object? obj)
        {
            return obj is ComponentType other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Handle;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentType<T> UnsafeCast<T>() => new(Handle);
    }

    public readonly struct ComponentType<T> : IEquatable<ComponentType<T>>
    {
        public readonly int Handle;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentType(int handle)
        {
            Handle = handle;
        }

        public static implicit operator ComponentType(in ComponentType<T> componentType)
        {
            return Unsafe.As<ComponentType<T>, ComponentType>(ref Unsafe.AsRef(in componentType));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentType<TTo> UnsafeCast<TTo>() => new(Handle);

        public bool Equals(ComponentType<T> other)
        {
            return Handle == other.Handle;
        }

        public override bool Equals(object? obj)
        {
            return obj is ComponentType<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Handle;
        }
    }
}