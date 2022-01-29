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
            if (!HasComponent(handle, type))
                return false;

            var board = ComponentTypeBoard.Boards[type.Handle];
            board.RemoveComponent(handle);
            return true;
        }
    }
}