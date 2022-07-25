using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core;
using revecs.Extensions.Generator.Components;
using revecs.Querying;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial struct ComponentA : ISparseComponent
{
    public int Value;
}
public partial struct ComponentB : ISparseComponent
{
    public int Value;
}

public partial struct ComponentC : ISparseComponent
{
    public int Value;
}

public partial struct MyQuery : IQuery<(
    Write<ComponentA> A,
    Write<ComponentB> B,
    Write<ComponentC> C)>
{

}

public class QueryTest : TestBase
{
    public void Test1()
    {
        var world = new RevolutionWorld();
        var compA = ComponentA.Type.GetOrCreate(world);
        for (var i = 0; i < 100_000; i++)
        {
            var ent = world.CreateEntity();
            world.AddComponent(ent, compA, default);
        }
    }

    public void Test2()
    {
        var world = new RevolutionWorld();
        for (var i = 0; i < 100_000; i++)
        {
            var ent = world.CreateEntity();
            world.AddComponentA(ent);
        }
    }
    
    public QueryTest(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void BenchmarkComponent()
    {
        void Bench(Action ac)
        {
            GC.Collect();
            
            var sw = new Stopwatch();
            sw.Start();
            ac();
            sw.Stop();
            output.WriteLine($"{ac.Method.Name} took {sw.Elapsed.TotalMilliseconds}ms");
        }

        for (var ok = 0; ok < 50; ok++)
        {
            output.WriteLine("Direct");
            for (var i = 0; i < 4; i++)
            {
                Bench(Test1);
            }

            output.WriteLine("Via Extension Methods");
            for (var i = 0; i < 4; i++)
            {
                Bench(Test2);
            }
            output.WriteLine("  "); 
        }
    }
    
    [Fact]
    public void BenchmarkQuery()
    {
        using var world = new RevolutionWorld();
        
        void Test1()
        {
            var query = new ArchetypeQuery(world, new[]
            {
                ComponentA.ToComponentType(world),
                ComponentB.ToComponentType(world),
                ComponentC.ToComponentType(world),
            });
            var accessorA = world.AccessSparseSet(query.All[0].UnsafeCast<ComponentA>());
            var accessorB = world.AccessSparseSet(query.All[1].UnsafeCast<ComponentB>());
            var accessorC = world.AccessSparseSet(query.All[2].UnsafeCast<ComponentC>());
            foreach (var entity in query.GetEnumerator())
            {
                accessorA[entity] = default;
                accessorB[entity] = default;
                accessorC[entity] = default;
            }
        }
        
        void Test2()
        {
            var query = new MyQuery(world);
            foreach (var entity in query)
            {
                entity.A = default;
                entity.B = default;
                entity.C = default;
            }
        }
        
        void Bench(Action ac)
        {
            GC.Collect();
            
            var sw = new Stopwatch();
            sw.Start();
            ac();
            sw.Stop();
            output.WriteLine($"{ac.Method.Name} took {sw.Elapsed.TotalMilliseconds}ms");
        }
        
        for (var i = 0; i < 10_000; i++)
        {
            var ent = world.CreateEntity();
            world.AddComponentA(ent);
            world.AddComponentB(ent);
            world.AddComponentC(ent);
        }

        for (var ok = 0; ok < 50; ok++)
        {
            output.WriteLine("Direct");
            for (var i = 0; i < 4; i++)
            {
                Bench(Test1);
            }

            output.WriteLine("Via Extension Methods");
            for (var i = 0; i < 4; i++)
            {
                Bench(Test2);
            }
            output.WriteLine("  "); 
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe ref struct Enumerator
    {
        [FieldOffset(0)]
        private byte padding;
        
        public bool MoveNext()
        {
            var ok = new SparseSetAccessor<int>();
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            
        }

        public ref readonly Enumerator Current
        {
            get
            {
                return ref *(Enumerator*) AsRef(ref this);
            }
        }
        
        public static void* AsRef(ref Enumerator yes)
        {
            return Unsafe.AsPointer(ref yes.padding);
        }
    }
}