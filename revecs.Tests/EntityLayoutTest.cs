using revecs.Core;
using revecs.Extensions.EntityLayout;
using revecs.Extensions.Generator.Components;
using revecs.Systems;
using revecs.Utility.Threading;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial class EntityLayoutTest : TestBase
{
    public partial struct EmptyComponent : ISparseComponent
    {
    }

    public partial struct LayoutTest : IEntityLayoutComponent
    {
        public void GetComponentTypes(RevolutionWorld world, List<ComponentType> componentTypes)
        {
            componentTypes.Add(EmptyComponent.Type.GetOrCreate(world));
        }
    }

    public EntityLayoutTest(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void TestSimple()
    {
        using var world = new RevolutionWorld();

        var ent = world.CreateEntity();
        world.AddComponent(ent, LayoutTest.Type.GetOrCreate(world));

        Assert.True(world.HasComponent(ent, LayoutTest.Type.GetOrCreate(world)));
        Assert.True(world.HasComponent(ent, EmptyComponent.Type.GetOrCreate(world)));
    }

    public readonly partial struct MyQuery : IQuery, With<LayoutTest> {}

    [RevolutionSystem]
    private static void CheckIfLayoutIsPresent<T>([Param] TaskCompletionSource tcs, [Query] MyQuery query)
    {
        if (query.Any())
            tcs.SetResult();
    }

    [Fact]
    public void TestSystem()
    {
        using var runner = new OpportunistJobRunner(0);
        using var world = new RevolutionWorld(runner);

        var ent = world.CreateEntity();
        world.AddComponent(ent, LayoutTest.Type.GetOrCreate(world));

        var tcs = new TaskCompletionSource();
        
        var group = new SystemGroup(world);
        group.Add(CheckIfLayoutIsPresent(tcs));
        
        runner.CompleteBatch(group.Schedule(runner));
        
        Assert.True(tcs.Task.IsCompletedSuccessfully);
    }
}