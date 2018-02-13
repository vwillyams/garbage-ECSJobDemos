namespace Unity.Properties
{
    public struct PropertyTreeNode
    {
        public IPropertyContainer Container;
        public readonly IProperty Property;

        public PropertyTreeNode(IPropertyContainer container, IProperty property)
        {
            Container = container;
            Property = property;
        }
    }
}