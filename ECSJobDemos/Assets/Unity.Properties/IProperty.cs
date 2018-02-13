using System;

namespace Unity.Properties
{
    public interface IProperty
    {
        string Name { get; }
        Type PropertyType { get; }
        bool IsReadOnly { get; }
        int GetVersion(IPropertyContainer container);
        object GetObjectValue(ref IPropertyContainer container);
        void SetObjectValue(ref IPropertyContainer container, object value);
        void Accept(IPropertyContainer container, IPropertyVisitor visitor);
    }

    public interface ITypedContainerProperty<TContainer>
        where TContainer : IPropertyContainer
    {
        void Accept(ref TContainer container, IPropertyVisitor visitor);
        int GetVersion(ref TContainer container);
    }

    public interface ITypedValueProperty<TValue> : IProperty
    {
        TValue GetValue(ref IPropertyContainer container);
        void SetValue(ref IPropertyContainer container, TValue value);
    }

    public interface IProperty<TContainer, TValue> : ITypedValueProperty<TValue>, ITypedContainerProperty<TContainer>
        where TContainer : IPropertyContainer
    {
        TValue GetValue(ref TContainer container);
        void SetValue(ref TContainer container, TValue value);
    }
}