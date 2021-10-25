using System.Diagnostics;
using System.Numerics;
using revecs.Core;
using revecs.Extensions.Generator.Commands;
using revecs.Extensions.Generator.Components;
using revecs.Systems;
using revtask.Core;
using revtask.OpportunistJobRunner;
using Xunit;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial class EntitySystemTest : TestBase
{
    public partial struct Position : ISparseComponent
    {
        public Vector3 Value;
    }

    public partial struct Velocity : ISparseComponent
    {
        public Vector3 Value;
    }

    public partial record struct PlayerQuery : IQuery<Write<Position>, Read<Velocity>>;

    public EntitySystemTest(ITestOutputHelper output) : base(output)
    {
    }

    [RevolutionSystem, DependOn(nameof(SpawnPlayer))]
    private static void MoveSystem(
        [Query] writeQuery
        <
            Write<Position>,
            Read<Velocity>
        > players)
    {
        foreach (var (pos, vel) in players)
        {
            pos.Value += vel.Value;
        }
    }

    [RevolutionSystem, DependOn(nameof(MoveSystem))]
    private static void ReadSystem([Query] readQuery<Read<Position>> players)
    {
        foreach (var (handle, pos) in players)
        {
            Console.WriteLine($"{handle}: {pos.Value}");
        }
    }


    [RevolutionSystem]
    private static void SpawnPlayer(
        [Param] Velocity baseVelocity,
        [Cmd] spawn<
            ICmdEntityAdmin,
            Position.Cmd.IAdmin,
            Velocity.Cmd.IAdmin
        > cmd)
    {
        var entity = cmd.CreateEntity();
        cmd.AddPosition(entity);
        cmd.AddVelocity(entity, baseVelocity);
    }

    [Fact(DisplayName = "Generated System")]
    public void TestSystemFromGenerated()
    {
        using var runner = new OpportunistJobRunner(1f);
        using var world = new RevolutionWorld(runner);

        var systemGroup = new SystemGroup(world);
        systemGroup.Add(SpawnPlayer(new Velocity {Value = {X = 8}}));
        systemGroup.Add(MoveSystem);
        systemGroup.Add(ReadSystem);

        runner.StartPerformanceCriticalSection();
        for (var i = 0; i < 10; i++)
        {
            output.WriteLine("\nIteration: " + i);
            var sw = new Stopwatch();
            sw.Start();
            runner.CompleteBatch(systemGroup.Schedule(runner));
            sw.Stop();
            output.WriteLine(sw.Elapsed.TotalMilliseconds.ToString());
        }

        var players = new PlayerQuery(world);
        var iter = players.Query.GetEntityCount();

        // Querying is always ordered (from lowest to highest)
        foreach (var (pos, vel) in players)
        {
            Assert.Equal(vel.Value.X * iter, pos.Value.X, 0.1);

            iter--;
        }
    }
}