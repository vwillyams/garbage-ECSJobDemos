using System;
using System.Reflection;

namespace Unity.Properties.Reflection
{
    public class ReflectedProperty<TValue> : IProperty<ReflectedPropertyContainer, TValue>
    {
        public PropertyInfo property { get; private set; }

        public string Name => property.Name;

        public Type PropertyType => typeof(TValue);

        public ReflectedProperty(PropertyInfo field)
        {
            property = field;
        }

        public bool IsReadOnly => property.GetSetMethod() == null;

        public int GetVersion(IPropertyContainer container)
        {
            return container.VersionStorage?.GetVersion(this, ref container) ?? 0;
        }
        
        public int GetVersion(ref ReflectedPropertyContainer container)
        {
            return container.VersionStorage?.GetVersion(this, ref container) ?? 0;
        }

        public object GetObjectValue(ref IPropertyContainer context)
        {
            var tc = (ReflectedPropertyContainer)context;
            return GetValue(ref tc);
        }

        public void SetObjectValue(ref IPropertyContainer container, object value)
        {
            var tc = (ReflectedPropertyContainer)container;
            SetValue(ref tc, (TValue) value);
        }

        public void Accept(IPropertyContainer container, IPropertyVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public TValue GetValue(ref IPropertyContainer container)
        {
            var tc = (ReflectedPropertyContainer)container;
            return GetValue(ref tc);
        }

        public void SetValue(ref IPropertyContainer container, TValue value)
        {
            var tc = (ReflectedPropertyContainer)container;
            SetValue(ref tc, value);
        }

        public TValue GetValue(ref ReflectedPropertyContainer container)
        {
            return (TValue) property.GetValue(container.instance);

        }

        public virtual void SetValue(ref ReflectedPropertyContainer container, TValue value)
        {
            property.SetValue(container.instance, value);
            if (container.VersionStorage != null)
                container.IncrementVersion(this, ref container);
        }

        public virtual void Accept(ref ReflectedPropertyContainer container, IPropertyVisitor visitor)
        {
            var typedVisitor = visitor as IPropertyVisitor<TValue>;

            var context = new VisitContext<TValue>
            {
                Property = this,
                Value = GetValue(ref container),
                Index = -1
            };

            if (null != typedVisitor)
            {
                typedVisitor.Visit(ref container, context);
            }
            else
            {
                visitor.Visit(ref container, context);
            }
        }

    }

    public class ReflectedField<TValue> : IProperty<ReflectedPropertyContainer, TValue>
    {
        private FieldInfo property { get; set; }
        
        public string Name => property.Name;
        public Type PropertyType => typeof(TValue);

        public ReflectedField(FieldInfo field)
        {
            property = field;
        }

        public bool IsReadOnly => property.IsLiteral || property.IsInitOnly;

        public int GetVersion(IPropertyContainer container)
        {
            return container.VersionStorage?.GetVersion(this, ref container) ?? 0;
        }

        public int GetVersion(ref ReflectedPropertyContainer container)
        {
            return container.VersionStorage?.GetVersion(this, ref container) ?? 0;
        }

        public object GetObjectValue(ref IPropertyContainer context)
        {
            var tc = (ReflectedPropertyContainer)context;
            return GetValue(ref tc);
        }

        public void SetObjectValue(ref IPropertyContainer container, object value)
        {
            var tc = (ReflectedPropertyContainer)container;
            SetValue(ref tc, (TValue)value);
        }

        public void Accept(IPropertyContainer container, IPropertyVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public TValue GetValue(ref IPropertyContainer container)
        {
            var tc = (ReflectedPropertyContainer)container;
            return GetValue(ref tc);
        }

        public void SetValue(ref IPropertyContainer container, TValue value)
        {
            var tc = (ReflectedPropertyContainer)container;
            SetValue(ref tc, value);
        }

        public TValue GetValue(ref ReflectedPropertyContainer container)
        {
            return (TValue)property.GetValue(container.instance);

        }

        public virtual void SetValue(ref ReflectedPropertyContainer container, TValue value)
        {
            property.SetValue(container.instance, value);
            if (container.VersionStorage != null)
                container.IncrementVersion(this, ref container);
        }

        public virtual void Accept(ref ReflectedPropertyContainer container, IPropertyVisitor visitor)
        {
            var typedVisitor = visitor as IPropertyVisitor<TValue>;

            var context = new VisitContext<TValue>
            {
                Property = this,
                Value = GetValue(ref container),
                Index = -1
            };

            if (null != typedVisitor)
            {
                typedVisitor.Visit(ref container, context);
            }
            else
            {
                visitor.Visit(ref container, context);
            }
        }
    }
}