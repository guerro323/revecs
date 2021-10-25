using System.Diagnostics;
using revecs.Core;
using revecs.Systems;
using revecs.Utility.Threading;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public class SystemTest : TestBase
{
    public SystemTest(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void TestSimpleSystem()
    {
        using var world = new RevolutionWorld();
        using var runner = new OpportunistJobRunner(1f);
        
        var systemGroup = new SystemGroup(world);
        systemGroup.Add(new MySystem());
        
        runner.CompleteBatch(systemGroup.Schedule(runner));
    }

    public class MySystem : ISystem
    {
        public bool Create(SystemHandle systemHandle, RevolutionWorld world)
        {
            return true;
        }

        public void PreQueue(SystemHandle systemHandle, RevolutionWorld world)
        {
        }

        public JobRequest Queue(SystemHandle systemHandle, RevolutionWorld world, IJobRunner runner)
        {
            Console.WriteLine($"Queuing with handle {systemHandle}");
            
            return runner.Queue(new Job());
        }

        struct Job : IJob
        {
            public int SetupJob(JobSetupInfo info) => 1;
            
            public void Execute(IJobRunner runner, JobExecuteInfo info)
            {
                Console.WriteLine("Hello World!");
            }
        }
    }
}