using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Components;
using revecs.Extensions.Buffers;
using revecs.Extensions.Generator.Components;
using revecs.Utility.Threading;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace revecs.Tests;

public partial class Tests : TestBase
{
    public partial struct GameTime : ISparseComponent
    {
        public float Delta;
        public double Elapsed;
        public int Frame;
    }

    public partial struct Position : ISparseComponent
    {
        public Vector3 Value;
    }

    public partial struct Velocity : ISparseComponent
    {
        public Vector3 Linear;
    }

    public partial struct BufferTest : IBufferComponent
    {
        public int Ok;
    }

    partial struct Time : IQuery<Read<GameTime>>, Singleton
    {

    }

    partial struct Players : IQuery<Write<Position>, Read<Velocity>>, None<GameTime>
    {
    }
    
    struct Batch01 : IJob
    {
        public bool[] Passed;
        
        public int SetupJob(JobSetupInfo info)
        {
            return Passed.Length;
        }

        public void Execute(IJobRunner runner, JobExecuteInfo info)
        {
            Passed[info.Index] = true;
        }
    }
    
    struct Batch02 : IJob
    {
        public bool[] Passed;
        
        public int SetupJob(JobSetupInfo info)
        {
            return Passed.Length;
        }

        public void Execute(IJobRunner runner, JobExecuteInfo info)
        {
            Passed[info.Index] = true;
        }
    }
    
    struct Batch03 : IJob, IJobExecuteOnCondition, IJobExecuteOnComplete
    {
        public bool[] Passed;
        
        public int SetupJob(JobSetupInfo info)
        {
            return Passed.Length;
        }

        public void Execute(IJobRunner runner, JobExecuteInfo info)
        {
            Passed[info.Index] = true;
        }

        public bool CanExecute(IJobRunner runner, JobExecuteInfo info)
        {
            if (!Passed[info.Index])
            {
                Passed[info.Index] = true;
                return false;
            }

            return true;
        }

        public void OnComplete(IJobRunner runner, Exception? exception)
        {
            Console.WriteLine("Completed");
        }
    }

    [Fact]
    public void MultipleTypeBatchTest()
    {
        using var runner = new OpportunistJobRunner(0.5f);
        var batches = new List<JobRequest>();
        for (var i = 0; i < 100; i++)
        {
            JobRequest b;
            if ((i % 3) == 0)
                b = runner.Queue(new Batch01() {Passed = new bool[i]});
            if ((i % 3) == 1)
                b = runner.Queue(new Batch02() {Passed = new bool[i]});
            else
                b = runner.Queue(new Batch03() {Passed = new bool[i]});

            batches.Add(b);
        }

        foreach (var batch in batches)
        {
            while (!runner.IsCompleted(batch))
            {
                Thread.Sleep(5);
            }
        }
        
        output.WriteLine("Finished");
    }

    [Fact]
    public void BatchTest()
    {
        using var runner = new OpportunistJobRunner(1f);
        var batches = new List<(JobRequest req, Batch01 batch)>();
        for (var i = 0; i < 10_000; i++)
        {
            var batch = new Batch01() {Passed = new bool[512]};
            batches.Add((runner.Queue(batch), batch));

        }

        foreach (var (req, batch) in batches)
        {
            while (!runner.IsCompleted(req))
            {
                Thread.Sleep(5);
            }

            foreach (var t in batch.Passed)
                Assert.True(t);
        }
    }

    [Fact]
    public void Create()
    {
        using var world = new RevolutionWorld();
        var timeSetup = new SparseComponentSetup<GameTime>();
        var positionSetup = new SparseComponentSetup<Position>();
        var velocitySetup = new SparseComponentSetup<Velocity>();

        var timeComponent = world.AsGenericComponentType<GameTime>(world.RegisterComponent(timeSetup));
        var positionComponent = world.AsGenericComponentType<Position>(world.RegisterComponent(positionSetup));
        var velocityComponent = world.AsGenericComponentType<Velocity>(world.RegisterComponent(velocitySetup));

        world.AddComponent(world.CreateEntity(), timeComponent, new GameTime {Delta = 0.5f, Elapsed = 1, Frame = 2});

        for (var i = 0; i < 10_000; i++)
        {
            var player = world.CreateEntity();
            world.AddComponent(player, positionComponent, default);
            world.AddComponent(player, velocityComponent, new Vector3(4, 0, 0));
        }

        var sw = new Stopwatch();
        var ts = TimeSpan.MaxValue;
        for (var i = 0; i < 100; i++)
        {
            sw.Restart();
            Update(new Time(world), new Players(world));
            sw.Stop();
            if (sw.Elapsed < ts)
                ts = sw.Elapsed;
            
            if ((i % 50) == 0)
                Thread.Sleep(10);
        }
        
        output.WriteLine("From generated: " + ts.TotalMilliseconds + "ms");

        ts = TimeSpan.MaxValue;
        for (var i = 0; i < 100; i++)
        {
            sw.Restart();
            UpdateManual(world, timeComponent, positionComponent, velocityComponent);
            sw.Stop();
            if (sw.Elapsed < ts)
                ts = sw.Elapsed;
            
            if ((i % 50) == 0)
                Thread.Sleep(10);
        }
        
        output.WriteLine("From manual code: " + ts.TotalMilliseconds + "ms");

        //output.WriteLine($"{world.GetComponentData(world.EntityBoard.GetEntities()[2], positionComponent).Value}");
    }
    
    void Update(Time timeQuery, Players playerQuery)
    {
        float delta = timeQuery.Delta;
        /*foreach (var (_, time) in timeQuery)
            delta = time.Delta;*/

        foreach (var (pos, vel) in playerQuery)
        {
            pos.Value += vel.Linear * delta;
        }
    }

    void UpdateManual(RevolutionWorld world, 
        ComponentType<GameTime> timeComponent,
        ComponentType<Position> positionComponent,
        ComponentType<Velocity> velocityComponent)
    {
        var timeQuery = new Time(world);
        var playerQuery = new Players(world);
                
        var delta = 0.5f;
        foreach (var handle in timeQuery.Query)
            delta = world.GetComponentData(handle, timeComponent).Delta;

        var positionAccessor = world.AccessSparseSet(positionComponent);
        var velocityAccessor = world.AccessSparseSet(velocityComponent);
                
        foreach (var arch in playerQuery.Query.GetMatchedArchetypes())
        {
            foreach (var handle in world.ArchetypeBoard.GetEntities(arch))
            {
                positionAccessor[handle].Value += velocityAccessor[handle].Linear * delta;
            }
        }
    }

    public Tests(ITestOutputHelper output) : base(output)
    {
    }
}