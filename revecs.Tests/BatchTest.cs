using revecs.Core;
using revecs.Core.Boards;
using revtask.Core;
using revtask.Helpers;
using revtask.OpportunistJobRunner;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial class BatchTest : TestBase
{
    struct EmptyJob : IJob
    {
        public int SetupJob(JobSetupInfo info)
        {
            return 1;
        }

        public void Execute(IJobRunner runner, JobExecuteInfo info)
        {
            // fake work
            Thread.Sleep(5);
        }
    }

    record struct WaitJob(JobRequest ParentJob) : IJob
    {
        public int SetupJob(JobSetupInfo info)
        {
            return 1;
        }

        public void Execute(IJobRunner runner, JobExecuteInfo info)
        {
            runner.CompleteBatch(ParentJob);
        }
    }

    [Fact]
    public void TestNonDeadlock()
    {
        using var runner = new OpportunistJobRunner(1f);

        var list = new List<JobRequest>();
        for (var i = 0; i < 100; i++)
        {
            var empty = new EmptyJob();
            var wait = new WaitJob(runner.Queue(new WaitJob(runner.Queue(empty))));
            list.Add(runner.Queue(wait));
        }

        var final = runner.WaitBatches(list);
        runner.CompleteBatch(final);
    }

    struct MultipleIndex : IJob
    {
        public int SetupJob(JobSetupInfo info)
        {
            return 2;
        }

        public void Execute(IJobRunner runner, JobExecuteInfo info)
        {
            Console.WriteLine($"{info.Index}; {info.MaxUseIndex}");
        }
    }

    [Fact]
    public void TestMultipleIndex()
    {
        using var runner = new OpportunistJobRunner(0.5f);

        foreach (var _ in Enumerable.Range(0, 10))
        {
            var batches = new List<JobRequest>();
            for (var i = 0; i < 100; i++)
                batches.Add(runner.Queue(new MultipleIndex()));

            runner.CompleteBatch(runner.WaitBatches(batches));
        }
    }

    public BatchTest(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void TestQuery()
    {
        using var runner = new OpportunistJobRunner(0.5f);
        using var world = new RevolutionWorld();
        world.AddBoard(nameof(BatchRunnerBoard), new BatchRunnerBoard(runner, world));

        const int count = 100;
        for (var i = 0; i < count; i++)
        {
            var ent = world.CreateEntity();
            world.AddComponentA(ent, new ComponentA {Value = i});
            world.AddComponentB(ent, new ComponentB {Value = i + 100});
        }
        
        world.ArchetypeUpdateBoard.Update();

        var query = new MyQuery(world);
        var hashsetA = new HashSet<int>();
        var hashsetB = new HashSet<int>();
        query.QueueAndComplete(runner, (_, entities) =>
        {
            foreach (var ent in entities)
            {
                output.WriteLine($"{ent.Handle} {ent.a.Value} {ent.b.Value}");
                lock (this)
                {
                    Assert.False(hashsetA.Contains(ent.a.Value), "hashsetA.Contains(ent.a.Value)");
                    Assert.False(hashsetB.Contains(ent.b.Value), "hashsetB.Contains(ent.b.Value)");
                    
                    hashsetA.Add(ent.a.Value);
                    hashsetB.Add(ent.b.Value);
                }
            }
        });

        for (var i = 0; i < count; i++)
        {
            Assert.Contains(i, hashsetA);
            Assert.Contains(i + 100, hashsetB);
        }
        
        output.WriteLine("ok?");
    }

    public partial struct MyQuery : IQuery<(Read<ComponentA> a, Read<ComponentB> b)>
    {
    }
}