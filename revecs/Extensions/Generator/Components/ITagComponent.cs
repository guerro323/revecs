namespace revecs.Extensions.Generator.Components;

public interface ITagComponent : IRevolutionComponent
{
    public const string Imports = "using revecs.Core.Components.Boards;\nusing revecs.Extensions.Generator;\nusing revecs.Core.Components;";
    
    public const string External = @"
    public static class [Type]Extensions
    {
        public static void Add[Type](this RevolutionWorld world, UEntityHandle entity) {
            world.AddComponent(entity, [TypeAddr].Type.GetOrCreate(world), default);
        } 

        public static bool Remove[Type](this RevolutionWorld world, UEntityHandle entity) {
            return world.RemoveComponent(entity, [TypeAddr].Type.GetOrCreate(world));
        } 
    }
";
 
    // The accessors are kinda useless on this type (since the calls would be the same without them)
    // But they serve as a helper for future component types (such as buffer which need custom accessors)
    private const string Type = @"
        public static ComponentType ToComponentType(RevolutionWorld world) => Type.GetOrCreate(world);

        public static class Type
        {
            public static ComponentType<[TypeAddr]> GetOrCreate(RevolutionWorld world)
            {
                var existing = world.GetComponentType(typeof([TypeAddr]).TypeHandle, ManagedTypeData<[TypeAddr]>.Name);
                if (existing.Equals(default))
                    existing = world.RegisterComponent<TagComponentSetup<[TypeAddr]>>();

                return world.AsGenericComponentType<[TypeAddr]>(existing);
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
        public bool Has[Type](UEntityHandle handle)
        {
            return World.HasComponent(handle, [Type]Type);
        }
"";

                bool Has[Type](UEntityHandle handle) => throw new NotImplementedException();
            }
            public interface IAdmin : IRevolutionCommand, IRead
            {
                public const string Init = @""
            [Type]Dependency_WriteCount += 1;
"";

                public const string Body = @""
        public void Add[Type](UEntityHandle handle)
        {
            World.AddComponent(handle, [Type]Type, default);
        }

        public bool Remove[Type](UEntityHandle handle)
        {
            return World.RemoveComponent(handle, [Type]Type);
        }
"";

                void Add[Type](UEntityHandle handle) => throw new NotImplementedException();
                bool Remove[Type](UEntityHandle handle) => throw new NotImplementedException();
            }
        }
";

    public const string Body = $@"
{Type}
{Commands}
";
}