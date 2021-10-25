using revecs.Core;
using revecs.Extensions.Generator.Components;

namespace revecs.Extensions.EntityLayout;

public interface IEntityLayoutComponent : IRevolutionComponent
{
    void GetComponentTypes(RevolutionWorld world, List<ComponentType> componentTypes);
    
    private const string Type = @"
        public static class Type
        {
            public static ComponentType GetOrCreate(RevolutionWorld world)
            {
                var existing = world.GetComponentType(ManagedTypeData<[TypeAddr]>.Name);
                if (existing.Equals(default))
                {
                    var list = new List<ComponentType>();
                    default([TypeAddr]).GetComponentTypes(world, list);

                    existing = revecs.Extensions.EntityLayout.GameWorldExtensions.RegisterLayout
                    (
                        world,
                        ManagedTypeData<[TypeAddr]>.Name,
                        CollectionsMarshal.AsSpan(list)
                    );
                }

                return existing;
            }

            public const bool DisableReferenceWrapper = true;
        }
";
    
    private const string Commands = @"

";
    
    public const string Imports = "using revecs.Core.Components.Boards;\nusing revecs.Extensions.Generator;";
    
    public const string Body = @$"
{Type}
{Commands}
";
}