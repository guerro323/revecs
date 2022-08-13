using revecs.Core;
using revecs.Extensions.Buffers;
using revecs.Extensions.RelativeEntity.Generator;
using revecs.Systems.Generator;
using Xunit.Abstractions;

namespace revecs.Tests;

public partial class RelativeTest : TestBase
{
    public RelativeTest(ITestOutputHelper output) : base(output)
    {
        var query = new MyQuery();
    }

    public partial struct MyDescription : IDescriptionComponent
    {
        
    }

    public partial struct MyQuery : IQuery<(Read<MyDescription>, Read<MyDescription.Relative>)>
    {
        
    }

    public partial struct MySystem : IRevolutionSystem
    {
        public void Constraints(in SystemObject sys)
        {
            
        }

        public void Body()
        {
            foreach (var iter in RequiredQuery(
                         Read<MyDescription>(),
                         Read<MyDescription.Relative>()
                     ))
            {
                
            }
        }
    }
}