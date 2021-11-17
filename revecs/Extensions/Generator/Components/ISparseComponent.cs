namespace revecs.Extensions.Generator.Components;

public interface ISparseComponent : IRevolutionComponent
{
    public const string Imports = "using revecs.Core.Components;\nusing revecs.Extensions.Generator;";
 
    // The accessors are kinda useless on this type (since the calls would be the same without them)
    // But they serve as a helper for future component types (such as buffer which need custom accessors)
    public const string Body = @"
        public static class Type
        {
            public static ComponentType<[TypeAddr]> GetOrCreate(RevolutionWorld world)
            {
                var existing = world.GetComponentType(ManagedTypeData<[TypeAddr]>.Name);
                if (existing.Equals(default))
                    existing = world.RegisterComponent<SparseComponentSetup<[TypeAddr]>>();

                return world.AsGenericComponentType<[TypeAddr]>(existing);
            }

            public const bool DisableReferenceWrapper = false;

            public const string AccessorAccess_FieldType = ""SparseSetAccessor<[TypeAddr]>"";
            public const string AccessorAccess_Init = ""[field] = [world].AccessSparseSet<[TypeAddr]>([componentType].UnsafeCast<[TypeAddr]>());"";
            public const string AccessorAccess_Access = ""[value] = [access] [field][[entity]]"";
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
        public ref [TypeAddr] Update[Type](in UEntityHandle handle)
        {
            return ref World.GetComponentData(handle, [Type]Type);
        }
"";

                ref [TypeAddr] Update[Type](in UEntityHandle handle) => throw new NotImplementedException();   
            }
            public interface IRead : IRevolutionCommand, __Internals.IBase 
            {
                public const string ReadAccess = @""            if ([Type]Dependency_WriteCount == 0) [Type]Dependency.AddReader(request);"";

                public const string Body = @""
        public bool Has[Type](in UEntityHandle handle)
        {
            return World.HasComponent(handle, [Type]Type);
        }

        public ref readonly [TypeAddr] Read[Type](in UEntityHandle handle)
        {
            return ref World.GetComponentData(handle, [Type]Type);
        }
"";

                ref readonly [TypeAddr] Read[Type](in UEntityHandle handle) => throw new NotImplementedException();
            }
            public interface IAdmin : IRevolutionCommand, IWrite, IRead
            {
                public const string Body = @""
        public UComponentReference Add[Type](in UEntityHandle handle, in [TypeAddr] data = default)
        {
            return World.AddComponent(handle, [Type]Type, data);
        }

        public bool Remove[Type](in UEntityHandle handle)
        {
            return World.RemoveComponent(handle, [Type]Type);
        }
"";

                UComponentReference Add[Type](in UEntityHandle handle, in [TypeAddr] data = default) => throw new NotImplementedException();
                
                bool Remove[Type](in UEntityHandle handle) => throw new NotImplementedException();
            }
        }
";
}