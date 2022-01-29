using revecs.Core;
using revecs.Extensions.Generator.Components;

namespace revecs.Extensions.EntityLayout;

public interface IEntityLayoutComponent : IRevolutionComponent
{
    void GetComponentTypes(RevolutionWorld world, List<ComponentType> componentTypes);
    
    private const string Type = @"
        public static class Type
        {
            public static ComponentType<[TypeAddr]> GetOrCreate(RevolutionWorld world)
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

                return existing.UnsafeCast<[TypeAddr]>();
            }

            public const bool DisableReferenceWrapper = true;
        }
";
    
    private const string Commands = @"
        public static class __Internals
        {
            public interface IBase : IRevolutionCommand 
            {
                public const string Variables = @""        public readonly ComponentType<[TypeAddr]> [Type]Type;
        private readonly SwapDependency [Type]Dependency;
        private readonly int [Type]Dependency_WriteCount;"";

                public const string Init = @""
            [Type]Type = [TypeAddr].Type.GetOrCreate(World);
            [Type]Dependency = World.GetComponentDependency([Type]Type);
            [Type]Dependency_WriteCount = 0;
"";

                public const string Dependencies = 
""[Type]Dependency_WriteCount > 0 ? [Type]Dependency.TrySwap(runner, request) : [Type]Dependency.IsCompleted(runner, request)"";
            }
        }

        public static class Cmd
        {
            public interface IRead : IRevolutionCommand, __Internals.IBase 
            {
                public const string ReadAccess = @""            if ([Type]Dependency_WriteCount == 0) [Type]Dependency.AddReader(request);"";

                public const string Body = @""
        public bool Has[Type](in UEntityHandle handle)
        {
            return World.HasComponent(handle, [Type]Type);
        }
"";

                bool Has[Type](in UEntityHandle handle) => throw new NotImplementedException();
            }
            public interface IAdmin : IRevolutionCommand, IRead
            {
                public const string Init = @""
            [Type]Dependency_WriteCount += 1;
"";

                public const string Body = @""
        public void Add[Type](in UEntityHandle handle)
        {
            World.AddComponent(handle, [Type]Type, default);
        }

        public bool Remove[Type](in UEntityHandle handle)
        {
            return World.RemoveComponent(handle, [Type]Type);
        }
"";

                void Add[Type](in UEntityHandle handle) => throw new NotImplementedException();
                bool Remove[Type](in UEntityHandle handle) => throw new NotImplementedException();
            }
        }
";
    
    public const string Imports = "using revecs.Core.Components.Boards;\nusing revecs.Extensions.Generator;";
    
    public const string Body = @$"
{Type}
{Commands}
";
}