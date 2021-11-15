# What is a query?
Queries are used to filter entities based on their current archetype.   
A query automatically update entity archetypes on each calls.

# How to create a query (Raw)
```cs
EntityQuery characterQuery = new EntityQuery(
    world,
    // The entities must have components that contains 'all' components.
    all: new ComponentType[] { characterComponent, positionComponent },
    // The entities must NOT have components contained in 'none' filter
    none: new ComponentType[] { enemyComponent },
    // The entities must atleast have one component contained in 'or' filter
    or: new ComponentType[] { }
);
```

## Usage
```cs
var positionAccessor = world.GetSparseAccessor(positionComponent);
foreach (var UEntityHandle entity in characterQuery)
{
    ref var position = ref positionAccessor[entity];
}
```

# How to create a query (Source Generator)
```cs
partial struct MyQuery : IQuery<
    Read<Position>,
    With<Character>
    None<Enemey>
> {}
```

## Usage
```cs
var query = new MyQuery(world);
foreach (var (entity, position) in query)
{
    var y = position.Y;

    // RPosition type -> original Position type
    ref var pos = ref position.__ref;
}
```

## Available filters
- Write<T>: Push T in the 'all' filter, and indicate that we will write and read to it
- Read<T>: Push T in the 'all' filter, and indicate that we will read to it
- With<T>: Push T in the 'all' filter, but don't read or write to it
- None<T>: Push T in the 'none' filter
- Or<T>: Push T in the 'or' filter

## Singleton
- ISingleton: Transform queries function for singleton usage (see bottom)

### Usage
```cs
partial struct GameTime : ISparseComponent
{
    public float Time, Delta;
}

partial struct TimeQuery : IQuery<Read<GameTime>>, ISingleton {}

var query = new TimeQuery(world);

var time = query.Time;
var delta = query.Delta;
```