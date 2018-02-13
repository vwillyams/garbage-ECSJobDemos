using System.Collections.Generic;

namespace Unity.Properties
{
    public class ContainerListProperty<TContainer, TValue, TItem> : ListProperty<TContainer, TValue, TItem>
        where TContainer : IPropertyContainer
        where TValue : IList<TItem>
        where TItem : IPropertyContainer
    {
        public ContainerListProperty(string name, GetValueMethod getValue, SetValueMethod setValue, CreateInstanceMethod createInstanceMethod = null) 
            : base(name, getValue, setValue, createInstanceMethod)
        {
        }

        public override void Accept(ref TContainer container, IPropertyVisitor visitor)
        {
            var value = GetValue(ref container);
            
            // Delegate the Visit implementation to the user
            if (TryUserAccept(ref container, visitor, value))
            {
                // User has handled the visit; early exit
                return;
            }
           
            var listContext = new ListContext<TValue> {Property = this, Value = value, Index = -1, Count = value.Count};

            if (visitor.BeginList(ref container, listContext))
            {
                var typedItemVisitor = visitor as IPropertyVisitor<TItem>;

                if (null != typedItemVisitor)
                {
                    for (var i=0; i<Count(container); i++)
                    {
                        var item = GetValueAtIndex(container, i);
                        
                        var context = new VisitContext<TItem>
                        {
                            // TODO: we have no property for items
                            Property = null,
                            Value = item,
                            Index = i
                        };

                        typedItemVisitor.Visit(ref container, context);
                    }
                }
                else
                {
                    var count = Count(container);
                    for (var i=0; i<count; i++)
                    {
                        var item = GetValueAtIndex(container, i);
                        var context = new SubtreeContext<TItem>
                        {
                            Property = this,
                            Value = item,
                            Index = i
                        };
                    
                        if (visitor.BeginSubtree(ref container, context))
                        {
                            item.PropertyBag.Visit(ref item, visitor);
                        }
                        visitor.EndSubtree(ref container, context);
                    }
                }
            }
            visitor.EndList(ref container, listContext);
        }
    }
}