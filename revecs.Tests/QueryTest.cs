using System.Diagnostics;
using revecs.Core;
using revecs.Extensions.Generator.Components;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial struct ComponentA : ISparseComponent {}
public partial struct ComponentB : ISparseComponent {}
public partial struct ComponentC : ISparseComponent {}

public partial struct MyQuery : IQuery<(
    Write<ComponentA> A,
    Read<ComponentB>,
    None<ComponentC>)>
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
    public void Benchmark()
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
}