namespace Unity.Properties
{
    public struct PropertyPath
    {
        public readonly string[] Parts;

        public PropertyPath(string[] parts)
        {
            Parts = parts;
        }

        public PropertyPath(string path)
            : this(path.Split('.'))
        {
        }

        public static PropertyPath FromParts(params string[] parts)
        {
            return new PropertyPath(parts);
        }

        public object Resolve(ref IPropertyContainer root)
        {
            var propertyNode = root.PropertyBag.ResolveProperty(root, this);
            return propertyNode.Property.GetObjectValue(ref propertyNode.Container);
        }
        
        public TValue Resolve<TValue>(ref IPropertyContainer root)
        {
            var propertyNode = root.PropertyBag.ResolveProperty(root, this);
            return ((ITypedValueProperty<TValue>)propertyNode.Property).GetValue(ref propertyNode.Container);
        }

        public override string ToString()
        {
            return string.Join(".", Parts);
        }
    }
}