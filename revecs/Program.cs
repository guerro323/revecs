using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Components;
using revecs.Core.Components.Boards;
using revecs.Extensions.Buffers;
using revecs.Extensions.EntityLayout;
using revecs.Extensions.LinkedEntity;
using revecs.Extensions.RelativeEntity;
using revecs.Utility.Threading;

namespace revecs
{
    public class Program
    {
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
                if (!Passed[info.Index])
                    throw new InvalidOperationException();
            }

            public void OnComplete(IJobRunner runner, Exception? exception)
            {
                //Console.WriteLine("completed");
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
        }
        
        struct JobOnCompletion : IJob
        {
            public int SetupJob(JobSetupInfo info)
            {
                return 1;
            }

            public void Execute(IJobRunner runner, JobExecuteInfo info)
            {
                Console.WriteLine("On Completion");
            }
        }
        
        public static void Main()
        {
            using var runner = new OpportunistJobRunner(0.5f);
            
            var batches = new List<JobRequest>();

            var sw0 = new Stopwatch();
            sw0.Start();
            for (var i = 0; i < 1_000; i++)
            {
                JobRequest b;
                if ((i % 3) == 0)
                    b = runner.Queue(new Batch01() {Passed = new bool[16]});
                else if ((i % 3) == 1)
                    b = runner.Queue(new Batch02() {Passed = new bool[16]});
                else
                    b = runner.Queue(new Batch03() {Passed = new bool[16]});
                
                batches.Add(b);
            }
            sw0.Stop();
            Console.WriteLine($"Time  took to queue: {sw0.Elapsed.TotalMilliseconds}ms");
            sw0.Reset();

            var wait = runner.WaitBatches(batches);
            var complete = runner.Queue(new WaitAndActJob<JobOnCompletion>(new(), CollectionsMarshal.AsSpan(batches)));

            sw0.Start();
            runner.CompleteBatch(complete);
            runner.CompleteBatch(wait);
            sw0.Stop();
            Console.WriteLine($"Time took to complete {sw0.Elapsed.TotalMilliseconds}ms");
            
            var count = 100_000;
            
            var entities = new UEntityHandle[count];
            var componentOut = new UComponentReference[count];

            var sw = new Stopwatch();

            var lowestCreateEntity = TimeSpan.MaxValue;
            var lowestCreateEntityBatched = TimeSpan.MaxValue;
            var lowestAddComponent = TimeSpan.MaxValue;
            var lowestAddComponentBatched = TimeSpan.MaxValue;
            
            for (var i = 0; i < 100; i++)
            {
                sw.Restart();
                CreateEntity(entities.Length);
                sw.Stop();

                if (lowestCreateEntity > sw.Elapsed)
                    lowestCreateEntity = sw.Elapsed;
                
                sw.Restart();
                CreateEntityBatched(entities);
                sw.Stop();

                if (lowestCreateEntityBatched > sw.Elapsed)
                    lowestCreateEntityBatched = sw.Elapsed;
                
                sw.Reset();
                AddComponent(sw, entities);

                if (lowestAddComponent > sw.Elapsed)
                    lowestAddComponent = sw.Elapsed;
                
                sw.Reset();
                AddComponentBatched(sw, entities, componentOut);

                if (lowestAddComponentBatched > sw.Elapsed)
                    lowestAddComponentBatched = sw.Elapsed;
                
                if ((i % 10) == 0)
                    Thread.Sleep(10);

                Console.WriteLine(i);
            }

            {
                using var world = new RevolutionWorld();
                var bufferType = world.AsBufferType<float>(world.RegisterComponent<BufferComponentSetup<float>>());

                var ent = world.CreateEntity();
                world.AddComponent(ent, bufferType.ComponentType);

                var buffer = world.ReadBuffer(ent, bufferType);
                buffer.Add(4.2f);
                buffer.Add(6.9f);

                foreach (var f in world.ReadComponent(ent, bufferType.Generic))
                {
                    Console.WriteLine(f);
                }

                var accessor = world.AccessComponentSet(bufferType.List);
                ref var bufferRef = ref accessor.FirstOrThrow(ent);

                foreach (var f in bufferRef)
                    Console.WriteLine(f);
            }

            {
                using var world = new RevolutionWorld();
                world.AddLinkedEntityModule();

                var a = world.CreateEntity();
                var b = world.CreateEntity();
                var c = world.CreateEntity();
                var d = world.CreateEntity();
                
                world.SetLink(b, a, true);
                world.SetLink(c, b, true);
                world.SetLink(d, c, true);
                world.SetLink(a, d, true);
                world.SetLink(d, a, true);
                world.SetLink(c, b, false);
                
                //world.DestroyEntity(a);

                Console.WriteLine($"a: {world.Exists(a)} {world.ReadParents(a).Length} {world.ReadChildren(a).Length}");
                Console.WriteLine($"b: {world.Exists(b)} {world.ReadParents(b).Length} {world.ReadChildren(b).Length}");
                Console.WriteLine($"c: {world.Exists(c)} {world.ReadParents(c).Length} {world.ReadChildren(c).Length}");
                Console.WriteLine($"d: {world.Exists(d)} {world.ReadParents(d).Length} {world.ReadChildren(d).Length}");
            }

            {
                using var world = new RevolutionWorld();
                world.AddRelativeEntityModule();

                var playerDescription = world.RegisterDescription("Player");

                var a = world.CreateEntity();
                var b = world.CreateEntity();

                world.AddComponent(b, playerDescription.Itself);
                world.AddRelative(playerDescription, a, b);

                world.TryGetRelative(playerDescription, a, out var parent);
                Console.WriteLine($"Entity A: {parent} {world.ReadOwnedRelatives(playerDescription, a).Length}");
                world.TryGetRelative(playerDescription, b, out parent);
                Console.WriteLine($"Entity B: {parent} {world.ReadOwnedRelatives(playerDescription, b).Length}");

                world.RemoveComponent(b, playerDescription.Itself);
                
                world.TryGetRelative(playerDescription, a, out parent);
                Console.WriteLine($"Entity A: {parent} {world.ReadOwnedRelatives(playerDescription, a).Length}");
                world.TryGetRelative(playerDescription, b, out parent);
                Console.WriteLine($"Entity B: {parent} {world.ReadOwnedRelatives(playerDescription, b).Length}");
                
                world.AddRelative(playerDescription, a, b);
                
                world.TryGetRelative(playerDescription, a, out parent);
                Console.WriteLine($"Entity A: {parent} {world.ReadOwnedRelatives(playerDescription, a).Length}");
                world.TryGetRelative(playerDescription, b, out parent);
                Console.WriteLine($"Entity B: {parent} {world.ReadOwnedRelatives(playerDescription, b).Length}");
                
                world.RemoveRelative(playerDescription, a);
                
                world.TryGetRelative(playerDescription, a, out parent);
                Console.WriteLine($"Entity A: {parent} {world.ReadOwnedRelatives(playerDescription, a).Length}");
                world.TryGetRelative(playerDescription, b, out parent);
                Console.WriteLine($"Entity B: {parent} {world.ReadOwnedRelatives(playerDescription, b).Length}");
                
                world.AddRelative(playerDescription, a, b);
                world.AddRelative(playerDescription, b, a);
                
                world.TryGetRelative(playerDescription, a, out parent);
                Console.WriteLine($"Entity A: {parent} {world.ReadOwnedRelatives(playerDescription, a).Length}");
                world.TryGetRelative(playerDescription, b, out parent);
                Console.WriteLine($"Entity B: {parent} {world.ReadOwnedRelatives(playerDescription, b).Length}");
                
                world.RemoveRelative(playerDescription, a);
                
                world.TryGetRelative(playerDescription, a, out parent);
                Console.WriteLine($"Entity A: {parent} {world.ReadOwnedRelatives(playerDescription, a).Length}");
                world.TryGetRelative(playerDescription, b, out parent);
                Console.WriteLine($"Entity B: {parent} {world.ReadOwnedRelatives(playerDescription, b).Length}");
            }

            {
                using var world = new RevolutionWorld();
                world.AddEntityLayoutModule();

                var componentTypeA = world.RegisterComponent("TypeA", new SparseSetComponentBoard(sizeof(int), world));
                var componentTypeB = world.RegisterComponent("TypeB", new SparseSetComponentBoard(sizeof(int), world));

                var layout = world.RegisterLayout("MyLayout", stackalloc[]
                {
                    componentTypeA,
                    componentTypeB
                });

                var entity = world.CreateEntity();

                world.AddComponent(entity, layout);

                Console.WriteLine($"has typeA: {world.HasComponent(entity, componentTypeA)}");
                Console.WriteLine($"has typeB: {world.HasComponent(entity, componentTypeB)}");

                world.RemoveComponent(entity, layout);

                Console.WriteLine($"has typeA: {world.HasComponent(entity, componentTypeA)}");
                Console.WriteLine($"has typeB: {world.HasComponent(entity, componentTypeB)}");

                world.AddComponent(entity, layout);
                world.AddComponent(entity, componentTypeB);

                Console.WriteLine($"has typeA: {world.HasComponent(entity, componentTypeA)}");
                Console.WriteLine($"has typeB: {world.HasComponent(entity, componentTypeB)}");

                world.RemoveComponent(entity, layout);

                Console.WriteLine($"has typeA: {world.HasComponent(entity, componentTypeA)}");
                Console.WriteLine($"has typeB: {world.HasComponent(entity, componentTypeB)}");

                world.AddComponent(entity, layout);

                Console.WriteLine($"has typeA: {world.HasComponent(entity, componentTypeA)}");
                Console.WriteLine($"has typeB: {world.HasComponent(entity, componentTypeB)}");

                world.RemoveComponent(entity, componentTypeA);
                world.GetArchetype(entity);

                Console.WriteLine($"has typeA: {world.HasComponent(entity, componentTypeA)}");
                Console.WriteLine($"has typeB: {world.HasComponent(entity, componentTypeB)}");
                Console.WriteLine($"has layout: {world.HasComponent(entity, layout)}");
            }

            Console.WriteLine($"\n For {entities.Length} Entities\n");

            Console.WriteLine($"CreateEntity - {lowestCreateEntity.TotalMilliseconds}ms");
            Console.WriteLine($"CreateEntityBatched - {lowestCreateEntityBatched.TotalMilliseconds}ms");
            Console.WriteLine($"AddComponent - {lowestAddComponent.TotalMilliseconds}ms");
            Console.WriteLine($"AddComponentBatched - {lowestAddComponentBatched.TotalMilliseconds}ms");
        }

        private static void CreateEntity(int length)
        {
            var world = new RevolutionWorld();
            for (var i = 0; i < length; i++)
            {
                world.CreateEntity();
            }
        }

        private static void CreateEntityBatched(UEntityHandle[] entities)
        {
            var world = new RevolutionWorld();
            world.CreateEntities(entities);
        }

        private static void AddComponent(Stopwatch sw, UEntityHandle[] entities)
        {
            var world = new RevolutionWorld();
            world.CreateEntities(entities);

            var positionType = world.RegisterComponent<SparseComponentSetup<Position>>();

            sw.Start();
            foreach (var ent in entities)
                world.AddComponent(ent, positionType);
            sw.Stop();
        }

        private static void AddComponentBatched(Stopwatch sw, UEntityHandle[] entities, UComponentReference[] output)
        {
            var world = new RevolutionWorld();
            world.CreateEntities(entities);

            var positionType = world.RegisterComponent<SparseComponentSetup<Position>>();
 
            sw.Start();
            world.AddComponentBatched(entities, output, positionType);
            sw.Stop();
        }

        private struct Position
        {
            public Vector3 Value;
        }
    }
}