using revecs.Core;

namespace revecs.Extensions.Generator.Components;

public interface IRevolutionComponent
{
    static abstract ComponentType ToComponentType(RevolutionWorld world);
}

public static class RevolutionWorldExtensions
{
    public static ComponentType ToComponentType<T>(this RevolutionWorld world)
        where T : IRevolutionComponent
    {
        return T.ToComponentType(world);
    }
}