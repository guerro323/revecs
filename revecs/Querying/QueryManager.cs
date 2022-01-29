using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core;

namespace revecs.Querying;

public class QueryManager
{
    internal readonly Branch Root;

    public QueryManager(RevolutionWorld world)
    {
        Root = new Branch {World = world};
    }

    public ProgressiveQuery Write<T>(out T enumValue)
    {
        return new ProgressiveQuery(Root).Write(out enumValue);
    }
    
    public ProgressiveQuery With<T>()
    {
        return new ProgressiveQuery(Root).With<T>();
    }
}

internal class Branch
{
    public RevolutionWorld World;
    public List<ComponentType> Types = new List<ComponentType>();
    public Dictionary<ComponentType, Branch> Map = new();
    public ArchetypeQuery? Result = null;

    public Branch GetNext(ComponentType type)
    {
        if (Map.TryGetValue(type, out var result))
        {
            Console.WriteLine("take old branch");
            return result;
        }

        result = new Branch
        {
            World = World
        };
        result.Types.AddRange(Types);
        result.Types.Add(type);
        Map[type] = result;

        return result;
    }

    public ArchetypeQuery GetResult()
    {
        if (Result != null)
            return Result;

        Result = new ArchetypeQuery(World, CollectionsMarshal.AsSpan(Types));
        return Result;
    }
}

public struct ProgressiveQuery
{
    private Branch _branch;
            
    internal ProgressiveQuery(Branch branch)
    {
        _branch = branch;
    }

    public ProgressiveQuery Write<T>(out T enumValue)
    {
        Unsafe.SkipInit(out enumValue);
        
        enumValue = ref Unsafe.NullRef<T>();
                    
        return new ProgressiveQuery(_branch.GetNext(default));
    }
    
    public ProgressiveQuery With<T>()
    {
        return new ProgressiveQuery(_branch.GetNext(default));
    }

    public ArchetypeQuery Result => _branch.GetResult();

    public Span<UEntityHandle>.Enumerator GetEnumerator() => throw new InvalidOperationException();
}