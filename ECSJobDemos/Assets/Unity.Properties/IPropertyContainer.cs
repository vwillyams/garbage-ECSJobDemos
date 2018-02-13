namespace Unity.Properties
{
    public interface IPropertyContainer
    {
        IVersionStorage VersionStorage { get; }
        IPropertyBag PropertyBag { get; }
    }

    interface ISubtreeContainer
    {
        IPropertyContainer GetSubtreeContainer(IPropertyContainer c, IProperty p);
    }
}
