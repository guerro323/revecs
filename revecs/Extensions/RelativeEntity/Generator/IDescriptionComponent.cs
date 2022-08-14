using revecs.Core;
using revecs.Extensions.Generator.Components;

namespace revecs.Extensions.RelativeEntity.Generator;

public interface IDescriptionComponent : IRevolutionComponent
{
    private const string RelativeType = @"
            public static ComponentType ToComponentType(RevolutionWorld world) => Type.GetOrCreate(world);

            public static class Type
            {
                public static ComponentType<UEntityHandle> GetOrCreate(RevolutionWorld world)
                {
                    var type = [TypeAddr].Type.GetOrCreate(world);
                    return revecs.Extensions.RelativeEntity.GameWorldExtensions.GetDescriptionType(world, type).Relative.UnsafeCast<UEntityHandle>();
                }

                public const bool DisableReferenceWrapper = true;

public const string AccessorAccess_FieldType = ""EntityComponentAccessor<UEntityHandle>"";
public const string AccessorAccess_Init = ""[field] = [world].AccessEntityComponent([componentType].UnsafeCast<UEntityHandle>());"";
public const string AccessorAccess_Access = ""[access] [field][[entity]][0]"";
public const string AccessorAccess_ValueType = ""UEntityHandle"";

public const string WorldAccess_Access = ""[value] = [access] [world].GetComponentData([entity], [componentType].UnsafeCast<UEntityHandle>())"";
public const string WorldAccess_ValueType = ""UEntityHandle"";

            }
";
    
    private const string Type = @"
        public static ComponentType ToComponentType(RevolutionWorld world) => Type.GetOrCreate(world);

        public static class Type
        {
            public static ComponentType<UEntityHandle> GetOrCreate(RevolutionWorld world)
            {
                revecs.Extensions.RelativeEntity.GameWorldExtensions.AddRelativeEntityModule(world);

                var existing = world.GetComponentType(ManagedTypeData<[TypeAddr]>.Name);
                if (existing.Equals(default))
                {
                    existing = revecs.Extensions.RelativeEntity.GameWorldExtensions.RegisterDescription
                    (
                        world,
                        ManagedTypeData<[TypeAddr]>.Name
                    ).Itself;
                }

                return existing.UnsafeCast<UEntityHandle>();
            }

            public const bool DisableReferenceWrapper = true;
            public const bool DisablePassByReference = true;

public const string AccessorAccess_FieldType = ""EntityComponentAccessor<UEntityHandle>"";
public const string AccessorAccess_Init = ""[field] = [world].AccessEntityComponent([componentType].UnsafeCast<UEntityHandle>());"";
public const string AccessorAccess_Access = ""[field][[entity]]"";
public const string AccessorAccess_ValueType = ""ReadOnlySpan<UEntityHandle>"";

public const string WorldAccess_Access = ""[value] = [world].ReadComponent([entity], [componentType].UnsafeCast<UEntityHandle>())"";
public const string WorldAccess_ValueType = ""ReadOnlySpan<UEntityHandle>"";
        }
";
    
        private const string RelativeCommands = @"
        public static class __Internals
        {
            public interface IBase : IRevolutionCommand 
            {
                public const string Variables = @""        public readonly ComponentType<UEntityHandle> [Type]RelativeType;
        private readonly SwapDependency [Type]RelativeDependency;
        private readonly int [Type]RelativeDependency_WriteCount;"";

                public const string Init = @""
            [Type]RelativeType = [TypeAddr].Relative.Type.GetOrCreate(World);
            [Type]RelativeDependency = World.GetComponentDependency([Type]RelativeType);
            [Type]RelativeDependency_WriteCount = 0;
"";

                public const string Dependencies = 
""[Type]RelativeDependency_WriteCount > 0 ? [Type]RelativeDependency.TrySwap(runner, request) : [Type]RelativeDependency.IsCompleted(runner, request)"";
            }
        }

        public static class Cmd
        {
            public interface IRead : IRevolutionCommand, __Internals.IBase 
            {
                public const string ReadAccess = @""            if ([Type]RelativeDependency_WriteCount == 0) [Type]RelativeDependency.AddReader(request);"";

                public const string Body = @""
        public bool Has[Type]Relative(in UEntityHandle handle)
        {
            return World.HasComponent(handle, [Type]RelativeType);
        }

        public UEntityHandle Read[Type]Relative(in UEntityHandle handle)
        {
            return World.GetComponentData(handle, [Type]RelativeType);
        }
"";

                UEntityHandle Read[Type]Relative(in UEntityHandle handle) => throw new NotImplementedException();
            }
            public interface IAdmin : IRevolutionCommand, IRead
            {
                public const string Init = @""
            [Type]RelativeDependency_WriteCount += 1;
"";

                public const bool WriteAccess = true;

                public const string Body = @""
        public void Add[Type]Relative(in UEntityHandle handle, in UEntityHandle target)
        {
            World.AddComponent(handle, [Type]RelativeType, target);
        }

        public bool Remove[Type]Relative(in UEntityHandle handle)
        {
            return World.RemoveComponent(handle, [Type]RelativeType);
        }
"";

                void Add[Type]Relative(in UEntityHandle handle, in UEntityHandle target) => throw new NotImplementedException();
                
                bool Remove[Type]Relative(in UEntityHandle handle) => throw new NotImplementedException();
            }
        }
";
        
    public const string External = @"
    public static class [Type]Extensions
    {
        public static bool Has[Type](this RevolutionWorld world, UEntityHandle entity) {
            return world.HasComponent(entity, [TypeAddr].Type.GetOrCreate(world));
        }

        public static void Add[Type](this RevolutionWorld world, UEntityHandle entity) {
            world.AddComponent(entity, [TypeAddr].Type.GetOrCreate(world), default);
        } 

        public static bool Remove[Type](this RevolutionWorld world, UEntityHandle entity) {
            return world.RemoveComponent(entity, [TypeAddr].Type.GetOrCreate(world));
        } 

        public static Span<UEntityHandle> Get[Type](this RevolutionWorld world, UEntityHandle entity) {
            return world.ReadComponent(entity, [TypeAddr].Type.GetOrCreate(world));
        } 
    }

    public static class [Type]RelativeExtensions
    {
        public static bool Has[Type]Relative(this RevolutionWorld world, UEntityHandle entity) {
            return world.HasComponent(entity, [TypeAddr].Relative.Type.GetOrCreate(world));
        }

        public static void Add[Type]Relative(this RevolutionWorld world, UEntityHandle entity, UEntityHandle target) {
            world.AddComponent(entity, [TypeAddr].Relative.Type.GetOrCreate(world), target);
        } 

        public static bool Remove[Type]Relative(this RevolutionWorld world, UEntityHandle entity) {
            return world.RemoveComponent(entity, [TypeAddr].Relative.Type.GetOrCreate(world));
        } 

        public static UEntityHandle Get[Type]Relative(this RevolutionWorld world, UEntityHandle entity) {
            return world.GetComponentData(entity, [TypeAddr].Relative.Type.GetOrCreate(world));
        } 
    }
";

    private const string Commands = @"
        public static class __Internals
        {
            public interface IBase : IRevolutionCommand 
            {
                public const string Variables = @""        public readonly ComponentType<UEntityHandle> [Type]Type;
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

        public Span<UEntityHandle> Read[Type](in UEntityHandle handle)
        {
            return World.ReadComponent(handle, [Type]Type);
        }
"";

                Span<UEntityHandle> Read[Type](in UEntityHandle handle) => throw new NotImplementedException();
            }
            public interface IAdmin : IRevolutionCommand, IRead
            {
                public const string Init = @""
            [Type]Dependency_WriteCount += 1;
"";

                public const bool WriteAccess = true;

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
        public struct Relative
        {{
{RelativeType}
{RelativeCommands}
        }}

{Type}
{Commands}
";
}