using revecs.Core;

namespace revecs.Extensions.EntityLayout;

public static class GameWorldExtensions
{
    public static void AddEntityLayoutModule(this RevolutionWorld world)
    {
        // ?
    }

    public static ComponentType RegisterLayout(this RevolutionWorld world,
        string name, ReadOnlySpan<ComponentType> componentTypeSpan)
    {
        var layoutBoard = new LayoutComponentBoard(componentTypeSpan.ToArray(), world);

        var componentType = world.RegisterComponent(name, layoutBoard);

        return componentType;
    }
}