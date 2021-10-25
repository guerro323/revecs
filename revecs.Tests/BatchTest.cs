using System.Text;
using revecs.Utility.Threading;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public class BatchTest : TestBase
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
}