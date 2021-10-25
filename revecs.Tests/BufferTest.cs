using revecs.Core;
using revecs.Extensions.Buffers;
using revecs.Extensions.Generator.Commands;
using revecs.Systems;
using revecs.Utility.Threading;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial class BufferTest : TestBase
{
    public partial struct BufferComponent : IBufferComponent
    {
        public int Value;
    }

    public BufferTest(ITestOutputHelper output) : base(output)
    {
    }

    [RevolutionSystem] 
    static void SpawnSystem([Cmd] c<ICmdEntityAdmin, BufferComponent.Cmd.IAdmin> cmd)
    {
        var entity = cmd.CreateEntity();
        cmd.AddBufferComponent(entity);
    }

    [RevolutionSystem, DependOn(nameof(SpawnSystem))]
    static void AddElementSystem([Param] int expectedValue,
        [Singleton] qw<
            Write<BufferComponent>
        > query)
    {
        query.BufferComponent.AddReinterpret(expectedValue);
        Console.WriteLine($"Add {expectedValue}");
    }

    [RevolutionSystem, DependOn(nameof(AddElementSystem))]
    static void ReadElementSystem([Param] int expectedValue,
        [Singleton] qr<
            Read<BufferComponent>
        > query)
    {
        Assert.NotEmpty(query.BufferComponent.ToArray());
        foreach (var b in query.BufferComponent.Reinterpret<int>())
        {
            Console.WriteLine($"Read {b}");
            Assert.Equal(expectedValue, b);
        }
    }

    [Fact]
    public void TestBuffer()
    {
        using var runner = new OpportunistJobRunner(1f);
        using var world = new RevolutionWorld(runner);

        const int value = 42;

        var group = new SystemGroup(world);
        group.Add(SpawnSystem);
        group.Add(AddElementSystem(value));
        group.Add(ReadElementSystem(value));
        
        runner.CompleteBatch(group.Schedule(runner));
    }
}