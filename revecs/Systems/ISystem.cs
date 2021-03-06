using revecs.Core;
using revtask.Core;

namespace revecs.Systems;

public interface ISystem
{
    bool Create(SystemHandle systemHandle, RevolutionWorld world);

    void PreQueue(SystemHandle systemHandle, RevolutionWorld world);
    
    JobRequest Queue(SystemHandle systemHandle, RevolutionWorld world, IJobRunner runner);
}