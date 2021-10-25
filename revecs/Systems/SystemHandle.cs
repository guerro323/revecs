using revecs.Core;

namespace revecs.Systems;

public record struct SystemHandle(int Id)
{
    public static implicit operator UEntityHandle(SystemHandle handle) => new(handle.Id);
    public static implicit operator SystemHandle(UEntityHandle handle) => new(handle.Id);
}