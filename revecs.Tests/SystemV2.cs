using revecs.Core;
using revecs.Extensions.Generator.Commands;
using revecs.Extensions.Generator.Components;
using revecs.Systems;
using revecs.Systems.Generator;
using revtask.Core;
using revtask.OpportunistJobRunner;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial struct Component : ISparseComponent
{
    public int Value;
}

partial struct CreateSystem : IRevolutionSystem,
    ICmdEntityAdmin,
    Component.Cmd.IAdmin
{
    public void Constraints(in SystemObject sys)
    {
    } 

    public void Body()
    {
        var entity = Cmd.CreateEntity();
        Cmd.AddComponent(entity, new Component {Value = 1});
    }
}

partial struct LogSystem : IRevolutionSystem,
    ICmdEntitySafe
{
    public void Constraints(in SystemObject sys)
    {
        sys.DependOn<CreateSystem>();
    }

    public void Body()
    {
        Console.WriteLine(RequiredResource<Component>().Value);
        foreach (var iter in RequiredQuery(Read<Component>()))
        {
            Console.WriteLine($"Entity {Cmd.Safe(iter.Handle)}, Value {iter.Component.Value}");
        }
    }
}

public class SystemV2Test : TestBase
{
    public SystemV2Test(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void TestSimpleSystem()
    {
        using var world = new RevolutionWorld();
        
        using var runner = new OpportunistJobRunner(1);

        var systemGroup = new SystemGroup(world);
        systemGroup.Add<CreateSystem>();
        systemGroup.Add<LogSystem>();
        
        runner.CompleteBatch(systemGroup.Schedule(runner));
    }
}