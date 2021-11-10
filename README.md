## Getting Started
### Without Source Generator
```c#

var world = new RevolutionWorld();
var myComponentType = world.RegisterComponentType<SparseSetComponentSetup<int>>();

var entity = world.CreateEntity();
world.AddComponentData(entity, myComponentType, 42);

var query = new EntityQuery(all: new[] { myComponentType });
var accessor = world.AccessSparseSet(myComponentType);
foreach (var entity in query)
{
    ref var value = ref accessor[entity];
}
```

### With Source Generator
```c#
partial struct MyComponent : ISparseComponent { public int Value; }
partial struct MyCommand : ICmd, MyComponent.Cmd.IAdmin, IEntityCmdAdmin {}
partial struct MyQuery : IQuery, Read<MyComponent> {}

var world = new RevolutionWorld();

var cmd = new MyCommand(world);
var query = new MyQuery(world);

var entity = cmd.CreateEntity();
cmd.AddMyComponent(entity, new MyComponent { Value = 42 });
// or if commands aren't needed
var entity = world.CreateEntity();
world.AddComponentData(entity, MyComponent.Type.GetOrCreate(world), new MyComponent { Value = 42 });

foreach (var (entity, comp) in query)
{
    ref var value = ref comp.Value;
}
```