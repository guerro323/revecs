using revecs.Systems;
using revecs.Utility.Threading;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public class DependencyTest : TestBase
{
    public DependencyTest(ITestOutputHelper output) : base(output)
    {
    }

    class Obj
    {
        public int Value;
    } 

    [Fact(Timeout = 2_000)]
    public void SimpleDependency()
    {
        using var runner = new OpportunistJobRunner(1f);
        runner.StartPerformanceCriticalSection();

        var dependency = new SwapDependency();
        var obj = new Obj();

        for (var i = 0; i < 100; i++)
        {
            var batches = new[]
            {
                runner.Queue(new Reader(obj, dependency)),
                runner.Queue(new Writer(16, obj, dependency)),
                runner.Queue(new Writer(8, obj, dependency)),
                runner.Queue(new Writer(42, obj, dependency)),
                runner.Queue(new Reader(obj, dependency)),
                runner.Queue(new Writer(41, obj, dependency)),
                runner.Queue(new Reader(obj, dependency))
            };

            foreach (var batch in batches)
            {
                while (!runner.IsCompleted(batch))
                    Thread.Sleep(0);
            }
        }
    }
    
    private record struct Reader(Obj Obj, SwapDependency Dependency) : IJob, IJobSetHandle, IJobExecuteOnCondition
    {
        public int SetupJob(JobSetupInfo info)
        {
            return 1;
        }

        public void Execute(IJobRunner runner, JobExecuteInfo info)
        {
            Console.WriteLine("[R] Start");
            var value = Obj.Value;
            Console.WriteLine("-- [R] Value: " + value);
            Console.WriteLine("[R] End");
            
            if (value != 16 && value != 8 && value != 42 && value != 41)
                Assert.Fail($"Expected 16,8,42,41 but had {value}");
            
            if (value != Obj.Value)
                Assert.Fail("Thread Race Condition");
        }

        public void SetHandle(IJobRunner runner, JobRequest handle)
        {
            Console.WriteLine($"Add {handle} As Reader");
            Dependency.AddReader(handle);
        }

        public bool CanExecute(IJobRunner runner, JobExecuteInfo info)
        {
            var dep = Dependency.GetWriterHandle();
            Console.WriteLine($"waiting for {dep}");
            return runner.IsCompleted(dep);
        }
    }
    
    private record struct Writer(int Set, Obj Obj, SwapDependency Dependency) : IJob, IJobExecuteOnCondition
    {
        public int SetupJob(JobSetupInfo info)
        {
            return 1;
        }

        public void Execute(IJobRunner runner, JobExecuteInfo info)
        {
            Console.WriteLine("[W] Start");
            Obj.Value = Set;
            Console.WriteLine("++ [W] Value: " + Obj.Value);
            Console.WriteLine("[W] End");
        }

        public bool CanExecute(IJobRunner runner, JobExecuteInfo info)
        {
            return Dependency.TrySwap(runner, info.Request);
        }
    }
}