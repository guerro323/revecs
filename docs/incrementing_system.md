# 1. Incrementing a component on every update 
1. The main section contains the init part, it will create our three systems, and execute them ten times.
2. The component `IntComponent` contains the `Value` field, which will be used for incrementation
3. The system `CreateEntitySystem` will create an entity with the `IntComponent` component. It will only be run once
4. The system `WriteSystem` will increment the newly created `IntComponent` by one.
5. The system `ReadSystem` will print the current value of `IntComponent`.
6. We make sure that `ReadSystem` update after `WriteSystem` which update after `CreateEntitySystem`.

```cs
using revecs;
using revecs.Core;
using revecs.Extensions.Generator.Commands;
using revecs.Extensions.Generator.Components;
using revecs.Systems;
using revtask.Core;
using revtask.OpportunistJobRunner;

namespace MyGame
{
    static partial class Program
    {
        public static void Main(string[] args)
        {
            // Create the job runner, this will execute our systems.
            // Assign 50% of the core to it
            var runner = new OpportunistJobRunner(0.5f);
            // Create the ECS world and pass the runner to it
            // This will contains the entities and our components.
            var world = new RevolutionWorld(runner);
            // Create a system group, it will contains our system and will schedule them
            var systemGroup = new SystemGroup(world);

            // Add our systems
            // The order here doesn't matter
            systemGroup.Add(new CreateEntitySystem());
            systemGroup.Add(new WriteSystem());
            systemGroup.Add(new ReadSystem());

            // Execute 10 times
            for (var i = 0; i < 10; i++)
            {
                runner.CompleteBatch(systemGroup.Schedule(runner));
            }
        }

        partial struct IntComponent : ISparseComponent
        {
            public int Value;
        }

        // This will create an entity with 'IntComponent' if it doesn't exist
        // This will only run once
        partial struct CreateEntitySystem : ISystem
        {
            private partial struct MyQuery : IQuery, With<IntComponent> {}
            private partial struct Commands : ICmdEntityAdmin, IntComponent.Cmd.IAdmin {}

            [RevolutionSystem]
            private static void Method(
                // Notice the Optional modifier, this mean that the system will still execute even if the query is empty
                [Query, Optional] MyQuery query,
                [Cmd] Commands cmd)
            {
                if (query.Any())
                    return;
                
                var entity = cmd.CreateEntity();
                cmd.AddIntComponent(entity);
            }
        }

        // This system will increment the component from the entity we created in 'CreateEntitySystem'
        // This will be done every frame
        partial struct WriteSystem : ISystem
        {
            [RevolutionSystem]
            [DependOn(typeof(CreateEntitySystem))] // This shall execute after CreateEntitySystem
            private static void Method([Query] q<Write<IntComponent>> query)
            {
                foreach (var (_, component) in query)
                {
                    component.Value++;
                }
            }
        }

        // This system will read our component and print it to the console
        partial struct ReadSystem : ISystem
        {
            [RevolutionSystem]
            [DependOn(typeof(WriteSystem))] // This shall execute after WriteSystem
            private static void Method([Query] q<Read<IntComponent>> query)
            {
                foreach (var (_, component) in query)
                {
                    Console.WriteLine(component.Value);
                }
            }
        }
    }
}
```