namespace Unity.Properties
{
    public class InheritedProperty<TBaseContainer, TContainer, TValue> : Property<TContainer, TValue>
        where TBaseContainer : class, IPropertyContainer
        where TContainer : class, TBaseContainer
    {
        public IProperty<TBaseContainer, TValue> BaseProperty { get; private set; }
        
        public InheritedProperty(IProperty<TBaseContainer, TValue> property) : base(property.Name,
            (ref TContainer c) =>
            {
                var typed = (TBaseContainer) c;
                return property.GetValue(ref typed);
            },
            (ref TContainer c, TValue v) =>
            {
                var typed = (TBaseContainer) c;
                property.SetValue(ref typed, v);
            })
        {
            BaseProperty = property;
        }

        public override void Accept(IPropertyContainer container, IPropertyVisitor visitor)
        {
            BaseProperty.Accept(container, visitor);
        }

        public override void Accept(ref TContainer container, IPropertyVisitor visitor)
        {
            var typed = (TBaseContainer) container;
            BaseProperty.Accept(ref typed, visitor);
        }
    }
}