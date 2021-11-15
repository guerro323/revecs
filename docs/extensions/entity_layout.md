# Entity Layout

An Entity Layout is a special type of component.    
It enforce the components of an entity, for example:
- A character layout would have Character, Position, Velocity components
- A player layout would have components related to a player (its name, player inputs, ...)

## Usage

```cs
var layout = world.RegisterLayout("CharacterLayout", new ComponentType[]
{
    characterType,
    positionType,
    velocityType
});

var entity = world.CreateEntity();
world.AddComponent(entity, layout);

world.HasComponent(entity, characterType); // will return true

// Doing so will remove the Characte, Position and Velocity component.
world.RemoveComponent(entity, layout);

// But if you wish to keep a component after a layout is destroyed, then you can override it:
world.AddComponent(entity, positionType);
world.AddComponent(entity, layout);
world.RemoveComponent(entity, layout);

world.HasComponent(entity, positionType); // will return true
world.HasComponent(entity, characterType); // will return false
```

## Source Generator

An entity layout component can be created from the SG:
```cs
public partial struct MyLayout : IEntityLayoutComponent
{
    public override void GetComponentTypes(RevolutionWorld world, List<ComponentType> componentTypes)
    {
        componentTypes.AddRange(new []
        {
            Character.Type.GetOrCreate(world),
            Position.Type.GetOrCreate(world),
            Velocity.Type.GetOrCreate(world)
        });
    }
}

var myLayout = MyLayout.Type.GetOrCreate(world);
```