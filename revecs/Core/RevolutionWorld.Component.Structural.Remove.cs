using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using revecs.Core.Components.Boards.Bases;

namespace revecs.Core
{
    public partial class RevolutionWorld
    {
        public bool RemoveComponent(UEntityHandle handle, ComponentType type)
        {
#if DEBUG
            ThrowOnInvalidHandle(handle);
#endif

            var board = ComponentTypeBoard.Boards[type.Handle];
            if (board.CustomEntityOperation)
            {
                var entityBoard = Unsafe.As<EntityComponentBoardBase>(board);
                var output = false;

                entityBoard.RemoveComponent(
                    MemoryMarshal.CreateSpan(ref handle, 1),
                    MemoryMarshal.CreateSpan(ref output, 1)
                );

                return output;
            }

            var linkedBoard = Unsafe.As<LinkedComponentBoardBase>(board);
            if (GameWorldLowLevel.RemoveComponentReference(
                linkedBoard,
                type,
                EntityComponentLinkBoard,
                handle
            ))
            {
                ArchetypeUpdateBoard.Queue(handle);
                return true;
            }

            return false;
        }
    }
}