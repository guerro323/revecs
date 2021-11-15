- [Entities](#Entities)
- [Components](#Components)
- [Systems](#Systems)

# Entities
Entities are the core of Revecs, they are the key to the data.
An entity can be directly created from a world via:
```cs
RevolutionWorld world;

UEntityHandle entity = world.CreateEntity();
```

It is also possible to create multiple entities in one batch:
```cs
Span<UEntityHandle> entities = stackalloc UEntityHandle[42];

world.CreateEntities(entities);
```

A `UEntityHandle` contains only the identifier to an entity,
this mean there is no difference between two different entities with the same identifier.  
To differentiate them, use `UEntitySafe`, you can get a safe version of an entity via:
```cs
UEntitySafe safe = world.Safe(handle);
```  
  

If you don't have access to the world, or you're in a system context,
then entities can still be created via [Commands](commands.md):  
```cs
partial struct Commands : ICmdEntityAdmin {}

private void Method(in Commands cmd)
{
    UEntityHandle entity = cmd.CreateEntity();
}
```
  
Entities can be destroyed via `DestroyEntity` or `DestroyEntities`.

# Components 

Components affect how an entity will viewed and used.
A component may or may not contains data.

Adding a component to an entity is simple:
```cs
ComponentType componentType;

ComponentReference component = world.AddComponent(entity, componentType);
```

It's also possible to pass data when adding a component:
```cs
ComponentType<Vector3> positionType;

world.AddComponent(entity, positionType, new Vector3(1, 2, 3));

// it's also possible to pass bytes
world.AddComponent(entity, positionType, new byte[] {  });
```

A component by default doesn't restrict its size, and can almost be of any type.  
So when reading a component, you may either read its data fully:
```cs
ComponentType componentType;

foreach (byte v in world.ReadComponent(entity, componentType))
{
    
}

// this may fail!
foreach (var v in world.ReadComponent<Vector3>(entity, componentType))
{
    
}
```  

Or you can use a helper if you're sure the component has only one element of this type:
```cs
ComponentType<Vector3> positionType;

ref var pos = ref world.GetComponentData(entity, positionType);
```

## Sparse Component
The most basic component type is the Sparse component.  
They're thighly packed for memory usage, and can be created/destroyed at high performance.  
They're not the fastest one for iterations since they require one constant array access and a random array access.  

**Creating a sparse component (raw)**
```cs
struct Position { public Vector3 Value; }

RevolutionWorld world;

ComponentType<Vector3> positionType = world.RegisterComponent<SparseComponentSetup<Position>, Vector3>();
```

**Creating a sparse component (source generator)**
```cs
partial struct Position : ISparseComponent { public Vector3 Value; }

RevolutionWorld world;

ComponentType<Vector3> positionType = Position.Type.GetOrCreate(world);
```

## Entity Component
The second type of component type is the Entity component.  
They're directly attached to an entity, but all implemention of an entity component can differ.
They're generally not fit for memory usage, but they're much more faster to create/destroy than sparse components.  
They are also faster than sparse component for iterations.  

An entity component can also be highly customized, it can go from buffer components, destruction chains to an archetype implementation.  

It's not possible (for now) to create a simple Entity component.

## Accessors
Accessors can be used to directly access to component data via an entity.

```cs
ComponentType<Vector3> positionType;

var positionAccessor = world.AccessSparseSet(positionType);

ref var pos = ref positionAccessor[entity];
```

The available accessors by default are:
- SparseSetAccessor (AccessSparseSet), can only access sparse sets.
- ComponentSetAccessor (AccessComponentSet), can access any type of component that support reading from component links.
- EntityComponentAccessor (AccessEntityComponent), can access any type of Entity Component.

## Commands with Source Generators
Source Generators simplify reading and adding components:
```cs
partial struct Commands : PositionType.Cmd.IAdmin {}

// yes you can use generics with commands
Vector3 GetPosition<T>(T cmd, UEntityHandle entity)
    where T : PositionType.Cmd.IRead
{
    return cmd.ReadPosition(entity).Value;
}

var cmd = new Commands(world);
cmd.AddPosition(entity, new Position() { Value = { X = 42 } });

Vector3 position = GetPosition(cmd, entity);
```

# Systems

A system process entities and components.  
By default in Revecs, a system is executed in parallel.  
It also doesn't contains a direct usage to the `RevolutionWorld` object,
but instead use Commands and Queries for managing entities.  

**!!! The syntax may change for creating systems !!!**  

It is recommended to create a system with source generators:  
- A generated system must be in a structure that implement ISystem
- It must have a method (whatever the name) marked with the `[RevolutionSystem]` attribute.
```cs
partial struct MySystem : ISystem
{
    [RevolutionSystem]
    private static void Method()
    {
        Console.WriteLine("Hello World!");
    }
}
```

You can now add your system in a `SystemGroup`:
```cs
var runner = new OpportunistJobRunner(0f);

var world = new RevolutionWorld(runner);
var systemGroup = new SystemGroup(world);

// Add the system...
systemGroup.Add(new MySystem());

// And run it!
runner.CompleteBatch(systemGroup.Schedule(runner));
```

This will output `Hello World!` to the console.

## Queries
You can add queries in your system:
- Create your query structure
- Add your query as a parameter, and mark it with the `[Query]` attribute.
```cs
partial struct Message : ISparseComponent { public string Value; }

partial struct MySystem : ISystem
{
    partial struct MyQuery : IQuery, Read<Message> {}

    [RevolutionWorld]
    private static void Method([Query] MyQuery query)
    {
        foreach (var (entity, message) in query)
        {
            Console.WriteLine(position.Value);
        }
    }
}
```

For more information on queries, see [Queries](queries.md)  

## Commands
You can add commands in your system:
- Create your command structure
- Add your command as a parameter, and mark it with the `[Cmd]` attribute.
```cs
partial struct MySystem : ISystem
{
    partial struct MyQuery : IQuery, Read<Message> {}
    partial struct MyCommand : ICmd, ICmdEntityAdmin, Message.Cmd.IAdmin {}

    [RevolutionWorld]
    private static void Method([Query] MyQuery query, [Cmd] MyCommand cmd)
    {
        var ent = cmd.CreateEntity();
        cmd.AddMessage(ent, new Message { Value = $"Hello World from {ent}" });

        foreach (var (entity, message) in query)
        {
            Console.WriteLine(position.Value);
        }
    }
}
```

For more information on commands, see [Commands](commands.md)  

## Dependencies and Update Order
By default, systems doesn't update in the order they were added.
To force an order, use either `DependOn` or `AddForeignDependency` attributes.

```cs
partial struct BeginSystem : ISystem {
    [RevolutionSystem]
    private static void Method() { }
}

partial struct MySystem : ISystem { 
    [RevolutionSystem, DependOn(typeof(BeginSystem)), AddForeignDependency(typeof(EndSystem))]
    private static void Method() { }
}

partial struct EndSystem : ISystem {
    [RevolutionSystem, DependOn(typeof(BeginSystem), true)]
    private static void Method() { }
}
```

This result in this order:
- BeginSystem
- MySystem
    - Will execute if BeginSystem was executed (no matter if it's sucessfull or not).
- EndSystem
    - Will only execute if BeginSystem was sucessfully executed.
    - And will execute if MySystem was executed (no matter if it's sucessfull or not).

The second parameter of `DependOn` indicate whether or not the system will run
 only if the dependency was sucessfully executed.  
The second parameter of `AddForeignDependency` indicate whether or not the target system will run
 only if the original system was sucessfully executed.

The dependency system can be used to created group as the example show (a better API may be added for the group pattern).  

And for more information on systems, see [Systems](systems.md)