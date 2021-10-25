namespace revecs.Core
{
    public readonly struct UComponentReference
    {
        public readonly ComponentType Type;
        public readonly UComponentHandle Handle;

        public UComponentReference(ComponentType type, UComponentHandle handle)
        {
            Type = type;
            Handle = handle;
        }
    }
}