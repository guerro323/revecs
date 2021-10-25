using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Utility;

namespace revecs.Query;

public class ArchetypeQuery : IDisposable
{
    public readonly RevolutionWorld World;

    public readonly ComponentType[] All;
    public readonly ComponentType[] None;
    public readonly ComponentType[] Or;

    private readonly List<UArchetypeHandle> _matchedArchetypes = new();
    private bool[] _archetypeIsValid = Array.Empty<bool>();

    private IDisposable _archetypeHandleListener;

    public ArchetypeQuery(RevolutionWorld world,
        Span<ComponentType> all = default,
        Span<ComponentType> none = default,
        Span<ComponentType> or = default)
    {
        World = world;

        All = all.ToArray();
        None = none.ToArray();
        Or = or.ToArray();

        _archetypeHandleListener = world.ArchetypeBoard.HandleUpdate.Subscribe(OnArchetypeAdded, true);
    }

    private BusySynchronizationManager _updateSync = new();

    private UArchetypeHandle _previous = new(1);
    private void OnArchetypeAdded(UArchetypeHandle _, UArchetypeHandle next)
    {
        using var sync = _updateSync.Synchronize();
        
        Array.Resize(ref _archetypeIsValid, next.Id + 1);

        var archetypeBoard = World.ArchetypeBoard;
        for (var i = _previous.Id; i <= next.Id; i++)
        {
            var archetype = new UArchetypeHandle(i);

            var matches = 0;
            var orMatches = 0;
            
            var componentSpan = archetypeBoard.GetComponentTypes(archetype);

            for (var comp = 0; comp != All.Length; comp++)
            {
                if (componentSpan.Contains(All[comp]))
                    matches++;
            }

            for (var comp = 0; comp != Or.Length; comp++)
            {
                if (componentSpan.Contains(Or[comp]))
                    orMatches++;
            }

            if (matches != All.Length || (Or.Length > 0 && orMatches == 0))
                continue;

            matches = 0;
            for (var comp = 0; comp != None.Length && matches == 0; comp++)
            {
                if (componentSpan.Contains(None[comp]))
                    matches++;
            }

            if (matches > 0)
                continue;
            
            _matchedArchetypes.Add(archetype);
            _archetypeIsValid[archetype.Id] = true;
        }

        _previous = new UArchetypeHandle(next.Id + 1);
    }

    // TODO: There should be a way to only update entities that can match this query
    private void update() => World.ArchetypeUpdateBoard.Update();
    
    /// <summary>
    ///     Get the entities from valid archetypes, with an option to swapback entities
    /// </summary>
    public ArchetypeQueryEnumerator GetEnumerator()
    {
        update();
        
        return new ArchetypeQueryEnumerator
        {
            Board = World.ArchetypeBoard,
            Inner = _matchedArchetypes,
            InnerIndex = -1,
            InnerSize = _matchedArchetypes.Count
        };
    }

    public Span<UArchetypeHandle> GetMatchedArchetypes() => CollectionsMarshal.AsSpan(_matchedArchetypes);

    // we need to make sure that the user know to not call this method at each iteration of a loop (eg: `for (i = 0; i < GetEntityCount(); i++)`)
    public int GetEntityCount()
    {
        update();
        
        // Maybe use ArchetypeUpdateBoard.PreSwitchEvent to calculate the entity count?
        
        var count = 0;
        
        foreach (var arch in CollectionsMarshal.AsSpan(_matchedArchetypes))
            count += World.ArchetypeBoard.GetEntities(arch).Length;
        
        return count;
    }

    public bool Any()
    {
        update();
        
        foreach (var arch in CollectionsMarshal.AsSpan(_matchedArchetypes))
            if (!World.ArchetypeBoard.GetEntities(arch).IsEmpty)
                return true;

        return false;
    }

    /// <summary>
    /// Get an entity at a specific index
    /// </summary>
    /// <param name="index">Index between 0 and <see cref="GetEntityCount"/></param>
    /// <returns>An entity handle</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when index is negative or superior/equal to <see cref="GetEntityCount"/></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public UEntityHandle EntityAt(int index)
    {
        update();
        
        if (index < 0)
            throw new IndexOutOfRangeException("Index is negative");

        var board = World.ArchetypeBoard;
        foreach (var archetypeHandle in CollectionsMarshal.AsSpan(_matchedArchetypes))
        {
            var span = board.GetEntities(archetypeHandle);

            index -= span.Length;
            if (index < 0)
                return span[index + span.Length];
        }

        throw new IndexOutOfRangeException($"{index} out of range {GetEntityCount()}");
    }

    /// <summary>
    /// Get entities from a ranged slice
    /// </summary>
    /// <param name="start">Start index</param>
    /// <param name="count">How much entities from start</param>
    /// <param name="outSpan">The sliced result</param>
    /// <returns>True if there is still entities when this method is called (in this case keep calling it until it's false)</returns>
    /// <remarks>This method should be called in a while loop with it as a condition since it may not fully return all entities in one call</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EntitySliceAt(ref int start, ref int count, out Span<UEntityHandle> outSpan)
    {
        update();
        
        if (count == 0)
        {
            outSpan = Span<UEntityHandle>.Empty;
            return false;
        }

        var board = World.ArchetypeBoard;
        var oldStart = start;
        foreach (var archetypeHandle in CollectionsMarshal.AsSpan(_matchedArchetypes))
        {
            var span = board.GetEntities(archetypeHandle);
            // Decrease start until it goes in the negative
            // The reason why it's like that is to not introduce another variable with the role of a counter
            start -= span.Length;
            if (start < 0)
            {
                // Get a slice of entities from start and count
                outSpan = span
                    .Slice(start + span.Length, Math.Min(span.Length - (start + span.Length), count));

                start = oldStart + outSpan.Length; // Next iteration will start on previous length

                // Decrease count by the result length
                // If it's superior than 0 this mean we need to go onto the next archetype
                count -= outSpan.Length;
                return count >= 0; // Stop if the list is exhausted (< 0) or continue if it's not
            }
        }

        outSpan = Span<UEntityHandle>.Empty;
        return false;
    }

    public void Dispose()
    {
        _matchedArchetypes.Clear();
        _archetypeIsValid = null!;

        _archetypeHandleListener.Dispose();
    }
}