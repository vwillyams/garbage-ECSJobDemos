using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Properties
{
    public class ListProperty<TContainer, TValue, TItem> : Property<TContainer, TValue>, IListProperty<TContainer, TItem>
        where TContainer : IPropertyContainer
        where TValue : IList<TItem>
    {
        public delegate TItem CreateInstanceMethod(ref TContainer c);

        private CreateInstanceMethod m_CreateInstanceMethod;
        
        public Type ItemType => typeof(TItem);
        
        public ListProperty(string name, GetValueMethod getValue, SetValueMethod setValue, CreateInstanceMethod createInstance = null) 
            : base(name, getValue, setValue)
        {
            m_CreateInstanceMethod = createInstance ?? DefaultCreateInstance;
        }

        private static TItem DefaultCreateInstance(ref TContainer unused)
        {
            Debug.Assert(typeof(TItem).IsValueType, $"List on container {typeof(TContainer)} of reference type {typeof(TItem)} should have their createInstanceMethod specified");
            return default(TItem);
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

            var itemTypeVisitor = visitor as IPropertyVisitor<TItem>;
            var listContext = new ListContext<TValue> {Property = this, Value = value, Index = -1, Count = value.Count};

            if (visitor.BeginList(ref container, listContext))
            {
                var itemVisitContext = new VisitContext<TItem>
                {
                    // TODO: we have no properties for list items
                    Property = null
                };

                for (var i = 0; i < Count(container); i++)
                {
                    var item = GetValueAtIndex(container, i);
                    itemVisitContext.Value = item;
                    itemVisitContext.Index = i;

                    if (null != itemTypeVisitor)
                    {
                        itemTypeVisitor.Visit(ref container, itemVisitContext);
                    }
                    else
                    {
                        visitor.Visit(ref container, itemVisitContext);
                    }
                }
            }
            visitor.EndList(ref container, listContext);
        }
        
        public int Count(IPropertyContainer container)
        {
            var list = GetValue(ref container);
            return list.Count;
        }

        public void Clear(IPropertyContainer container)
        {
            var list = GetValue(ref container);
            list.Clear();
            container.VersionStorage?.IncrementVersion(this, ref container);
        }
        
        public IEnumerator<TItem> GetEnumerator(TContainer container)
        {
            return GetValue(container).GetEnumerator();
        }
        
        public void AddObject(IPropertyContainer container)
        {
            var typed = (TContainer) container;
            Add(typed, m_CreateInstanceMethod(ref typed));
        }

        public void AddObject(IPropertyContainer container, object item)
        {
            if (item is IConvertible)
            {
                Add((TContainer) container, (TItem) Convert.ChangeType(item, typeof(TItem)));
            }
            else
            {
                Add((TContainer) container, (TItem) item);
            }
        }
        
        public virtual void Add(TContainer container, TItem item)
        {
            var list = GetValue(container);
            list.Add(item);
            container.VersionStorage?.IncrementVersion(this, ref container);
        }

        public void Clear(TContainer container)
        {
            var list = GetValue(container);
            list.Clear();
            container.VersionStorage?.IncrementVersion(this, ref container);
        }

        public bool Contains(TContainer container, TItem item)
        {            
            var list = GetValue(ref container);
            return list.Contains(item);
        }

        public bool Remove(TContainer container, TItem item)
        {
            var list = GetValue(ref  container);
            var result = list.Remove(item);
            container.VersionStorage?.IncrementVersion(this, ref container);
            return result;
        }

        public int IndexOf(TContainer container, TItem item)
        {
            var list = GetValue(ref container);
            return list.IndexOf(item);
        }

        public void Insert(TContainer container, int index, TItem item)
        {
            var list = GetValue(ref container);
            list.Insert(index, item);
            container.VersionStorage?.IncrementVersion(this, ref container);
        }

        public void RemoveAt(IPropertyContainer container, int index)
        {
            var list = GetValue(ref container);
            list.RemoveAt(index);
            container.VersionStorage?.IncrementVersion(this, ref container);
        }
        
        public object GetObjectValueAtIndex(IPropertyContainer container, int index)
        {
            var list = GetValue(ref container);
            return list[index];
        }
        
        public void SetObjectValueAtIndex(IPropertyContainer container, int index, object value)
        {
            var list = GetValue(ref container);

            if (Equals(list[index], (TItem) value))
            {
                return;
            }
            
            list[index] = (TItem) value;
            container.VersionStorage?.IncrementVersion(this, ref container);
        }

        public TItem GetValueAtIndex(TContainer container, int index)
        {
            var list = GetValue(ref container);
            return list[index];
        }
        
        public void SetValueAtIndex(TContainer container, int index, TItem value)
        {
            var list = GetValue(ref container);
            
            if (Equals(list[index], value))
            {
                return;
            }
            
            list[index] = value;
            container.VersionStorage?.IncrementVersion(this, ref container);
        }
        
        private static bool Equals(TItem a, TItem b)
        {
            if (null == a && null == b)
            {
                return true;
            }

            return null != a && a.Equals(b);
        }
    }
}