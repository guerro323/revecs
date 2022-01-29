using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Core.Components;
using revecs.Core.Components.Boards;
using revecs.Extensions.Buffers;
using revecs.Extensions.EntityLayout;
using revecs.Extensions.LinkedEntity;
using revecs.Extensions.RelativeEntity;
using revecs.Querying;
using revtask.Core;
using revtask.Helpers;
using revtask.OpportunistJobRunner;

namespace revecs
{
    public unsafe class Program
    {
        public static int SuperValue = 1;

        public static void TryOut(out int val)
        {
            Unsafe.SkipInit(out val);
            Console.WriteLine((IntPtr) Unsafe.AsPointer(ref Unsafe.Add(ref val, new IntPtr(-8))));
            
            val = ref SuperValue;
            Console.WriteLine((IntPtr) Unsafe.AsPointer(ref val));
        }
        
        public static void Main()
        {
            throw null;
            
            //ref var val = ref Unsafe.NullRef<int>();
            var val = 0;
            Console.WriteLine((IntPtr) Unsafe.AsPointer(ref Unsafe.Add(ref val, new IntPtr(0))));

            var offset = new IntPtr(-0);
            Console.WriteLine((IntPtr) Unsafe.AsPointer(ref Unsafe.Add(ref val, offset)));

            var ptr = (IntPtr) Unsafe.AsPointer(ref SuperValue);
            Unsafe.Copy(Unsafe.AsPointer(ref Unsafe.Add(ref val, offset)), ref ptr);
            SuperValue = 69;
            
            //TryOut(out val);
            Console.WriteLine(val);
            val = 42;
            Console.WriteLine(val);
            Console.WriteLine(SuperValue);
        }
        
        public static void AMain()
        {
            var proco = Process.Start(
                new ProcessStartInfo(
                    "nim", 
                    "--eval:\"import macros; echo parseExpr(\"\"echo bonjour\"\").treeRepr\" ")
                {
                    RedirectStandardOutput = true
                }
            );
            Console.WriteLine(proco.StandardOutput.ReadToEnd());

            using var runner = new OpportunistJobRunner(0.5f);

            var count = 100_000;
            
            var entities = new UEntityHandle[count];

            var sw = new Stopwatch();

            var lowestCreateEntity = TimeSpan.MaxValue;
            var lowestCreateEntityBatched = TimeSpan.MaxValue;
            var lowestAddComponent = TimeSpan.MaxValue;

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

                var accessor = world.AccessEntityComponent(bufferType.List);
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
                
                world.DestroyEntity(a);

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
        
        private struct Position
        {
            public Vector3 Value;
        }
    }
}