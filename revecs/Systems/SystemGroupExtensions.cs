namespace revecs.Systems;

public static class SystemGroupExtensions
{
    // This is used by generators
    public static void Add(this SystemGroup systemGroup, Action<SystemGroup> addSystemFactory)
    {
        addSystemFactory(systemGroup);
    }
}