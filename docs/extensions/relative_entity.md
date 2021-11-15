# Relative Entities

Relative Entities are used to link the relation between an entity and another one.  
For example: 
- A character entity would be linked to its player entity
- A bullet created by a character would be linked to the character.

## Usage

```cs
// Add the module first to the world (once)
world.AddRelativeEntityModule();

// Create the relation component (once)
var playerDescription = world.RegisterDescription("PlayerDescription");

var playerEntity = world.CreateEntity();
// It's not obligated to add the component since it can be implicitly added by children
world.AddComponent(playerEntity, playerDescription.Itself);

var characterEntity = world.CreateEntity();
world.AddComponent(characterEntity, playerDescription.Relative, playerEntity);
// or via extension methods
world.AddRelative(playerDescription, characterEntity, playerEntity);

// get parent
world.TryGetRelative(playerDescription, characterEntity, out playerEntity);

// read children
foreach (UEntityHandle handle in world.ReadOwnedRelatives(playerDescription, playerEntity))
{
    // contains characterEntity
}
```