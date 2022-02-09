using revecs.Core;
using revecs.Extensions.Generator.Components;
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
    public QueryTest(ITestOutputHelper output) : base(output)
    {
        var query = new MyQuery(null);
        query.First();

        var world = new RevolutionWorld();
        world.GetComponentA(default);
    }
}