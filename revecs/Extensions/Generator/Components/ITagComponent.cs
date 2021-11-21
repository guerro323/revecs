namespace revecs.Extensions.Generator.Components;

public interface ITagComponent : IRevolutionComponent
{
    public const string Imports = "using revecs.Core.Components.Boards;\nusing revecs.Extensions.Generator;\nusing revecs.Core.Components;";
 
    // The accessors are kinda useless on this type (since the calls would be the same without them)
    // But they serve as a helper for future component types (such as buffer which need custom accessors)
    private const string Type = @"
        public static class Type
        {
            public static ComponentType<[TypeAddr]> GetOrCreate(RevolutionWorld world)
            {
                var existing = world.GetComponentType(ManagedTypeData<[TypeAddr]>.Name);
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
        public UComponentReference Add[Type](in UEntityHandle handle)
        {
            return World.AddComponent(handle, [Type]Type, default);
        }

        public bool Remove[Type](in UEntityHandle handle)
        {
            return World.RemoveComponent(handle, [Type]Type);
        }
"";

                UComponentReference Add[Type](in UEntityHandle handle) => throw new NotImplementedException();
                bool Remove[Type](in UEntityHandle handle) => throw new NotImplementedException();
            }
        }
";

    public const string Body = $@"
{Type}
{Commands}
";
}