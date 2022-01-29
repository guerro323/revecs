using System.Runtime.CompilerServices;

namespace revecs.Core
{
    public partial class RevolutionWorld
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UArchetypeHandle GetArchetype(UEntityHandle entityHandle)
        {
            var archetypeSpan = EntityBoard.Archetypes;
            if (entityHandle.Id >= archetypeSpan.Length)
                throw new InvalidOperationException();

            // Archetype updates are delayed, but since the user want to know the archetype
            // we need to update
            ArchetypeUpdateBoard.Update(entityHandle);

            return archetypeSpan[entityHandle.Id];
        }
    }
}