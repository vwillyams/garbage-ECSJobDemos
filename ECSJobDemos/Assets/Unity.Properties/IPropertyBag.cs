using System.Collections.Generic;

namespace Unity.Properties
{
    public interface IPropertyBag
    {
        int PropertyCount { get; }
        IEnumerable<IProperty> Properties { get; }
        IEnumerable<PropertyTreeNode> Traverse(IPropertyContainer container, bool includeChildren);
        IProperty FindProperty(string name);
        PropertyTreeNode ResolveProperty(IPropertyContainer container, PropertyPath path);

        bool Visit<TContainer>(ref TContainer container, IPropertyVisitor visitor) 
            where TContainer : IPropertyContainer;
    }
}