using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Boards;

namespace revecs.Querying;

// TODO: option without ref struct (async usage)
public unsafe ref struct ArchetypeQueryEnumerator
{
    public ArchetypeBoard Board;
    public List<UArchetypeHandle> Inner;
    public int InnerIndex;
    public int InnerSize;

    private UEntityHandle current;

    public ref UEntityHandle Current => ref *(UEntityHandle*) Unsafe.AsPointer(ref current);

    private int index;
    private bool canMove;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MoveInnerNext()
    {
        index = 0;

        InnerIndex++;
        canMove = InnerIndex < InnerSize;

        if (canMove)
        {
            entities = Board.GetEntities(Inner[InnerIndex]);
            return true;
        }

        return false;
    }

    private Span<UEntityHandle> entities;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        /*// If the user set Current to default, this mean we need to decrease the index
        // TODO: It is still necessary? If one remove a component or delete the entity, it would only take place on the newest ArchetypeUpdateBoard call
        //       (this can be necessary for nested queries)
        if (current.Id == default)
            index--;*/
        
        while (index >= entities.Length)
        {
            if (!MoveInnerNext())
                return false;
        }
        
        current = Unsafe.Add(ref MemoryMarshal.GetReference(entities), index);
        index += 1;

        return true;
    }

    public ArchetypeQueryEnumerator GetEnumerator()
    {
        return this;
    }

    public UEntityHandle First
    {
        get
        {
            while (MoveNext())
                return Current;
            return default;
        }
    }
}