using revecs.Core;
using revecs.Extensions.Generator;

namespace revecs.Extensions.LinkedEntity.Generator;

// ReSharper disable InconsistentNaming
public interface __LinkedEntityCommandBase 
    // ReSharper restore InconsistentNaming
{
    public const string Variables = @"
        private readonly SwapDependency EntityLinkParentDependency;
        private readonly SwapDependency EntityLinkChildrenDependency;
        private readonly ComponentType EntityLinkChildrenType;
        private readonly ComponentType EntityLinkParentType;
        private readonly int EntityLink_WriteCount;";
    
    public const string Init = @"
            revecs.Extensions.LinkedEntity.GameWorldExtensions.AddLinkedEntityModule(World);

            var childrenName = revecs.Extensions.LinkedEntity.GameWorldExtensions.ChildrenComponent;
            var parentName = revecs.Extensions.LinkedEntity.GameWorldExtensions.ParentComponent;

            EntityLinkChildrenType = World.GetComponentType(childrenName);
            EntityLinkParentType = World.GetComponentType(parentName);

            EntityLinkChildrenDependency = World.GetComponentDependency(EntityLinkChildrenType);
            EntityLinkParentDependency = World.GetComponentDependency(EntityLinkParentType);

            EntityLink_WriteCount = 0;
";

    public const string Dependencies = @"EntityLink_WriteCount > 0 
? (EntityLinkChildrenDependency.TrySwap(runner, request) && EntityLinkParentDependency.TrySwap(runner, request)) 
: (EntityLinkChildrenDependency.IsCompleted(runner, request) && EntityLinkParentDependency.IsCompleted(runner, request))";
}

public interface ICmdLinkedEntityAdmin : IRevolutionCommand, 
    __LinkedEntityCommandBase,
    ICmdLinkedEntityRead
{
    public const string Body = @"
        public void AddEntityLink(UEntityHandle child, UEntityHandle owner) => revecs.Extensions.LinkedEntity.GameWorldExtensions.SetLink(World, child, owner, true);
        public void RemoveEntityLink(UEntityHandle child, UEntityHandle owner) => revecs.Extensions.LinkedEntity.GameWorldExtensions.SetLink(World, child, owner, false);
";

    public const string Init = @"
        EntityLink_WriteCount += 1;
";

    void AddEntityLink(UEntityHandle child, UEntityHandle owner) =>
        throw new NotImplementedException(nameof(AddEntityLink));

    void RemoveEntityLink(UEntityHandle child, UEntityHandle owner) =>
        throw new NotImplementedException(nameof(RemoveEntityLink));
}

public interface ICmdLinkedEntityRead : IRevolutionCommand,
    __LinkedEntityCommandBase
{
    public const string Body = @"
        public Span<UEntityHandle> ReadLinkedParents(UEntityHandle handle) => revecs.Extensions.LinkedEntity.GameWorldExtensions.ReadParents(World, handle);
        public Span<UEntityHandle> ReadLinkedChildren(UEntityHandle handle) => revecs.Extensions.LinkedEntity.GameWorldExtensions.ReadChildren(World, handle);
";
    
    public const string ReadAccess = @"if (EntityLink_WriteCount == 0) { EntityLinkChildrenDependency.AddReader(request); EntityLinkParentDependency.AddReader(request); }";

    Span<UEntityHandle> ReadLinkedParents(UEntityHandle handle) =>
        throw new NotImplementedException(nameof(ReadLinkedParents));

    Span<UEntityHandle> ReadLinkedChildren(UEntityHandle handle) =>
        throw new NotImplementedException(nameof(ReadLinkedChildren));
}