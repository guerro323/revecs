using revecs.Core;

namespace revecs.Extensions.Generator.Commands;

// ReSharper disable InconsistentNaming
public interface __EntityCommandBase 
    // ReSharper restore InconsistentNaming
{
    public const string Variables = @"
        private readonly SwapDependency EntityDependency;
        private readonly int Entity_WriteCount;";
    
    public const string Init = @"
            EntityDependency = World.GetEntityDependency();
            Entity_WriteCount = 0;
";

    public const string Dependencies = "Entity_WriteCount > 0 ? EntityDependency.TrySwap(runner, request) : EntityDependency.IsCompleted(runner, request)";
}

public interface ICmdEntityAdmin : IRevolutionCommand,
    __EntityCommandBase,
    // Admin give the rights for entity read commands
    ICmdEntityExists
{
    public const string Body = @"
        public UEntityHandle CreateEntity() => World.CreateEntity();
        public void DestroyEntity(UEntityHandle handle) => World.DestroyEntity(handle);
";

    public const string Init = @"
        Entity_WriteCount += 1;
";
    
    UEntityHandle CreateEntity() => throw new NotImplementedException(nameof(CreateEntity));
    void DestroyEntity(UEntityHandle handle) => throw new NotImplementedException(nameof(DestroyEntity));
}

public interface ICmdEntityExists : IRevolutionCommand, 
    __EntityCommandBase
{
    public const string Body = @"
        public bool Exists(UEntityHandle handle) => World.Exists(handle);
        public bool Exists(UEntitySafe handle) => World.Exists(handle);
";

    public const string ReadAccess = @"if (Entity_WriteCount == 0) EntityDependency.AddReader(request);";
    
    public bool Exists(UEntityHandle handle) => throw new NotImplementedException(nameof(Exists));
    public bool Exists(UEntitySafe handle) => throw new NotImplementedException(nameof(Exists));
}