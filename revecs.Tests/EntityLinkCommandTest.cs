using revecs.Core;
using revecs.Extensions.LinkedEntity.Generator;
using revecs.Systems;
using revecs.Utility.Threading;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial class EntityLinkCommandTest : TestBase
{
    public EntityLinkCommandTest(ITestOutputHelper output) : base(output)
    {
    }

    partial struct SetLinkCmd : ICmdLinkedEntityAdmin {}
    partial struct ReadLinkCmd : ICmdLinkedEntityRead {}
    
    [RevolutionSystem]
    private static void SetLink(
        [Param] UEntityHandle child, 
        [Param] UEntityHandle parent, 
        [Cmd] SetLinkCmd cmd)
    {
        cmd.AddEntityLink(child, parent);
    }

    [RevolutionSystem, DependOn(nameof(SetLink))]
    private static void ReadLink(
        [Param] TaskCompletionSource<string> errorTcs,
        [Param] UEntityHandle child,
        [Param] UEntityHandle parent,
        [Cmd] ReadLinkCmd cmd)
    { 
        if (cmd.ReadLinkedParents(child).IsEmpty || !cmd.ReadLinkedParents(child).Contains(parent))
            errorTcs.SetResult($"Child doesn't contains parent");
        else if (cmd.ReadLinkedChildren(parent).IsEmpty || !cmd.ReadLinkedChildren(parent).Contains(child))
            errorTcs.SetResult($"Parent doesn't contains child");
    }

    [Fact]
    public void EntityLinkTest()
    {
        using var runner = new OpportunistJobRunner(0);
        using var world = new RevolutionWorld(runner);

        var tcs = new TaskCompletionSource<string>();

        var child = world.CreateEntity();
        var parent = world.CreateEntity();
        
        var group = new SystemGroup(world);
        group.Add(SetLink((child, parent)));
        group.Add(ReadLink((tcs, child, parent)));

        runner.CompleteBatch(group.Schedule(runner));

        if (tcs.Task.IsCompleted)
        {
            Assert.Fail(tcs.Task.Result);
        }
    }
}