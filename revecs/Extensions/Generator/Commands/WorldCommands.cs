using revecs.Core;
using revecs.Systems.Generator;

namespace revecs.Extensions.Generator.Commands;

/// <summary>
/// Get access to the world
/// </summary>
/// <remarks>
/// Any <see cref="IRevolutionSystem"/> implementing this will have their permission highly elevated.
/// The world permission is ultimate, it's the highest one.
/// </remarks>
public interface IWorldCommand
{
    public const string Variables = @"
        private readonly SwapDependency WorldDependency;";
    
    public const string Init = @"
            WorldDependency = World.GetWorldDependency();
";

    public const string Dependencies = "WorldDependency.TrySwap(runner, request)";
    
    public const string Body = @"
        public RevolutionWorld GetWorld() => World;
";
    
    RevolutionWorld GetWorld() => throw new NotImplementedException(nameof(GetWorld));
}