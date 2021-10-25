namespace revecs.Core.Components
{
    public interface IComponentSetup
    {
        ComponentType Create(RevolutionWorld revolutionWorld);
    }
}