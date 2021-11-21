using revecs.Extensions.RelativeEntity.Generator;
using revecs.Systems;

namespace revecs.Tests;

public partial class RelativeTest
{
    interface IRevolutionSystem
    {
        void Body();

        void AddToSystemGroup(SystemGroup group)
        {
            
        }
    }
    
    partial struct Test : IDescriptionComponent
    {
        
    } 
    
    partial struct ReadItself : IQuery, Read<Test> {}
    partial struct ReadRelative : IQuery, Read<Test.Relative> {}
    
    partial struct CommandItself : Test.Cmd.IAdmin {}
    partial struct CommandRelative : Test.Relative.Cmd.IAdmin {}

    public void Ok()
    {
        var cmdItself = new CommandItself();

        var cmdRelative = new CommandRelative();
        
        

        var readItself = new ReadItself(null);
        
        
        foreach (var (ent, itself) in readItself)
        {
            foreach (var owned in itself)
            {
            }
        }
        
        var readRelative = new ReadRelative(null);
        foreach (var (ent, relative) in readRelative)
        {
            
        }
    }
}