using revecs.Core;
using revecs.Querying;
using revecs.Systems;

namespace revecs.Extensions.Generator;

public interface IRevolutionQuery
{
    ArchetypeQuery Query { get; }

    void ParallelOnEntities<T>(ReadOnlySpan<UEntityHandle> span, SystemState<T> state, object action);
}