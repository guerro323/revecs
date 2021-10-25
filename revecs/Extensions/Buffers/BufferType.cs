using System.Runtime.CompilerServices;
using revecs.Core;
using revecs.Core.Components;
using revecs.Utility;

namespace revecs.Extensions.Buffers;

public struct BufferType<T> where T : struct
{
    public ComponentType ComponentType;
    public ComponentType<T> Generic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return new(ComponentType.Handle); }
    }

    public ComponentType<BufferData<T>> List
    {
        get { return new ComponentType<BufferData<T>>(ComponentType.Handle);  }
    }

    public BufferType(ComponentType componentType)
    {
        ComponentType = componentType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ComponentType<T>(BufferType<T> o) => new(o.ComponentType.Handle);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ComponentType(BufferType<T> o) => new(o.ComponentType.Handle);
}

public struct BufferComponentSetup<T> : IComponentSetup
{
    public ComponentType Create(RevolutionWorld revolutionWorld)
    {
        return revolutionWorld.RegisterComponent(
            ManagedTypeData<T>.Name,
            new ComponentBufferBoard(ManagedTypeData<T>.Size, revolutionWorld)
        );
    }
}