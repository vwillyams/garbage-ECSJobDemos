namespace Unity.Properties
{
    public class ContainerProperty<TContainer, TValue> : Property<TContainer, TValue> 
        where TContainer : IPropertyContainer 
        where TValue : IPropertyContainer
    {
        public ContainerProperty(string name, GetValueMethod getValue, SetValueMethod setValue) : base(name, getValue, setValue)
        {
        }

        public override void Accept(ref TContainer container, IPropertyVisitor visitor)
        {
            var value = GetValue(ref container);

            // Delegate the Visit implementaton to the user
            if (TryUserAccept(ref container, visitor, value))
            {
                // User has handled the visit; early exit
                return;
            }
            
            var subtreeContext = new SubtreeContext<TValue> {Property = this, Value = value, Index = -1};
            if (visitor.BeginSubtree(ref container, subtreeContext))
            {
                value?.PropertyBag.Visit(ref value, visitor);
            }
            visitor.EndSubtree(ref container, subtreeContext);
        }
    }
}