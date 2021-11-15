## Linked Entity

Linked entities are used for destruction chains.    
If Entity A is linked to Entity B, and B is destroyed, then A will get destroyed too.

## Usage

```cs
// Add the module first in your application (once)
world.AddLinkedEntityModule();

var a = world.CreateEntity();
var b = world.CreateEntity();

world.SetLink(child: a, owner: b, true);

foreach (var parent in world.ReadParents(a))
{
    // contains B
}

foreach (var child in world.ReadChildren(b))
{
    // contains A
}

world.DestroyEntity(b);
world.Exists(a); // return false
```