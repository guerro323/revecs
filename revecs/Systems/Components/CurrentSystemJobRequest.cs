using revecs.Core;
using revecs.Core.Components;
using revecs.Utility;
using revtask.Core;

namespace revecs.Systems;

public struct CurrentSystemJobRequest
{
    public JobRequest Request;

    public static ComponentType<JobRequest> GetComponentType(RevolutionWorld world)
    {
        var name = ManagedTypeData<CurrentSystemJobRequest>.Name;

        var componentType = world.GetComponentType<JobRequest>(name);
        if (componentType.Equals(default))
            componentType = world.RegisterComponent<SparseComponentSetup<CurrentSystemJobRequest>, JobRequest>();

        return componentType;
    }
}