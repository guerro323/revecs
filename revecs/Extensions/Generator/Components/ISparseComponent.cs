namespace revecs.Extensions.Generator.Components;

public interface ISparseComponent : IRevolutionComponent
{
    public const string Imports = @"using revecs.Core.Components;
using revecs.Extensions.Generator;";

    public const string External = @"
    public static class [Type]Extensions
    {
        public static bool Has[Type](this RevolutionWorld world, UEntityHandle entity) {
            return world.HasComponent(entity, [TypeAddr].Type.GetOrCreate(world));
        }

        public static void Add[Type](this RevolutionWorld world, UEntityHandle entity, [TypeAddr] data = default) {
            world.AddComponent(entity, [TypeAddr].Type.GetOrCreate(world), data);
        } 

        public static bool Remove[Type](this RevolutionWorld world, UEntityHandle entity) {
            return world.RemoveComponent(entity, [TypeAddr].Type.GetOrCreate(world));
        } 

        public static ref [TypeAddr] Get[Type](this RevolutionWorld world, UEntityHandle entity) {
            return ref world.GetComponentData(entity, [TypeAddr].Type.GetOrCreate(world));
        } 
    }
";
    
    // The accessors are kinda useless on this type (since the calls would be the same without them)
    // But they serve as a helper for future component types (such as buffer which need custom accessors)
    public const string Body = @"
        public static ComponentType ToComponentType(RevolutionWorld world) => Type.GetOrCreate(world);

        public static class Type
        {
            public static ComponentType<[TypeAddr]> GetOrCreate(RevolutionWorld world)
            {
                var existing = world.GetComponentType(typeof([TypeAddr]).TypeHandle, ManagedTypeData<[TypeAddr]>.Name);
                if (existing.Equals(default))
                    existing = world.RegisterComponent<SparseComponentSetup<[TypeAddr]>>();

                return world.AsGenericComponentType<[TypeAddr]>(existing);
            }

            public const bool DisableReferenceWrapper = false;

            public const string AccessorAccess_FieldType = ""SparseSetAccessor<[TypeAddr]>"";
            public const string AccessorAccess_Init = ""[field] = [world].AccessSparseSet<[TypeAddr]>([componentType].UnsafeCast<[TypeAddr]>());"";
            public const string AccessorAccess_Access = ""[access] [field][[entity]]"";
            public const string AccessorAccess_ValueType = ""[TypeAddr]"";

            public const string WorldAccess_Access = ""[value] = [access] [world].GetComponentData([entity], [componentType].UnsafeCast<[TypeAddr]>())"";
            public const string WorldAccess_ValueType = ""[TypeAddr]"";
        }

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
            public interface IWrite : IRevolutionCommand, IRead, __Internals.IBase 
            {
                public const string Init = @""
            [Type]Dependency_WriteCount += 1;
"";

                public const bool WriteAccess = true;

                public const string Body = @""
        public ref [TypeAddr] Update[Type](UEntityHandle handle)
        {
            return ref World.GetComponentData(handle, [Type]Type);
        }
"";

                ref [TypeAddr] Update[Type](UEntityHandle handle) => throw new NotImplementedException();   
            }
            public interface IRead : IRevolutionCommand, __Internals.IBase 
            {
                public const string ReadAccess = @""            if ([Type]Dependency_WriteCount == 0) [Type]Dependency.AddReader(request);"";

                public const string Body = @""
        public bool Has[Type](UEntityHandle handle)
        {
            return World.HasComponent(handle, [Type]Type);
        }

        public ref readonly [TypeAddr] Read[Type](UEntityHandle handle)
        {
            return ref World.GetComponentData(handle, [Type]Type);
        }
"";

                bool Has[Type](UEntityHandle handle) => throw new NotImplementedException();
                ref readonly [TypeAddr] Read[Type](UEntityHandle handle) => throw new NotImplementedException();
            }
            public interface IAdmin : IRevolutionCommand, IWrite, IRead
            {
                public const string Body = @""
        public void Add[Type](UEntityHandle handle, in [TypeAddr] data = default)
        {
            World.AddComponent(handle, [Type]Type, data);
        }

        public bool Remove[Type](UEntityHandle handle)
        {
            return World.RemoveComponent(handle, [Type]Type);
        }
"";

                void Add[Type](UEntityHandle handle, in [TypeAddr] data = default) => throw new NotImplementedException();
                
                bool Remove[Type](UEntityHandle handle) => throw new NotImplementedException();
            }
        }
";
}