using System;
using System.Collections.Generic;

namespace Unity.Properties
{
    public interface IListProperty : IProperty
    {
        Type ItemType { get; }
        void AddObject(IPropertyContainer container);
        void AddObject(IPropertyContainer container, object value); 
        int Count(IPropertyContainer container);
        object GetObjectValueAtIndex(IPropertyContainer container, int index);
        void SetObjectValueAtIndex(IPropertyContainer container, int index, object value);
        void RemoveAt(IPropertyContainer container, int index);
        void Clear(IPropertyContainer container);
    }

    public interface ITypedContainerListProperty<in TContainer> : IListProperty
    {
    }

    public interface IListProperty<in TContainer, TItem> : ITypedContainerListProperty<TContainer>
        where TContainer : IPropertyContainer
    {
        TItem GetValueAtIndex(TContainer container, int index);
        void SetValueAtIndex(TContainer container, int index, TItem value);

        IEnumerator<TItem> GetEnumerator(TContainer container);
        void Add(TContainer container, TItem item);
        bool Contains(TContainer container, TItem item);
        bool Remove(TContainer container, TItem item);
        int IndexOf(TContainer container, TItem value);
        void Insert(TContainer container, int index, TItem value);
    }
}