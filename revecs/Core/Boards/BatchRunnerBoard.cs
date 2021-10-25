using revtask.Core;

namespace revecs.Core.Boards;

public class BatchRunnerBoard : BoardBase
{
    public readonly IJobRunner Runner;
    
    public BatchRunnerBoard(IJobRunner runner, RevolutionWorld world) : base(world)
    {
        Runner = runner;
    }

    public override void Dispose()
    {
    }
}