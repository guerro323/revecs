using revecs.Core;
using revecs.Extensions.Generator.Components;
using revecs.Systems;
using revecs.Utility.Threading;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial class ShortCircuitTest : TestBase
{
    public ShortCircuitTest(ITestOutputHelper output) : base(output)
    {
    }
    
    public partial struct EmptyComponent : ISparseComponent {}

    [RevolutionSystem]
    private static void MethodThatMustNotRun([Query] q<Read<EmptyComponent>> query)
    {
        if (query.Any())
            Assert.Fail("Query Must Be Empty");
        
        Assert.Fail("Method must not have been run");
    }
    
    [RevolutionSystem, DependOn(nameof(MethodThatMustNotRun), true)]
    private static void DependenceThatMustNotRun()
    {
        Assert.Fail($"{nameof(DependenceThatMustNotRun)} has been executed, which shouldn't be the case");
    }
    
    [RevolutionSystem, DependOn(nameof(MethodThatMustNotRun), false)]
    private static void DependenceThatCanRun([Param] TaskCompletionSource tcs)
    {
        tcs.SetResult();
    }

    [Fact]
    public void Test()
    {
        using var runner = new OpportunistJobRunner(0);
        using var world = new RevolutionWorld(runner);
        
        var tcs = new TaskCompletionSource();
        
        var group = new SystemGroup(world);
        group.Add(MethodThatMustNotRun);
        group.Add(DependenceThatMustNotRun);
        group.Add(DependenceThatCanRun(opt: tcs));
        
        runner.CompleteBatch(group.Schedule(runner));
        
        Assert.True(tcs.Task.IsCompletedSuccessfully);
    }
} 