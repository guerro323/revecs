using revecs.Core;

namespace revecs.Extensions.RelativeEntity;

public readonly struct DescriptionType
{
    public readonly ComponentType Itself;
    public readonly ComponentType Relative;

    public DescriptionType(ComponentType itself, ComponentType relative)
    {
        Itself = itself;
        Relative = relative;
    }
}