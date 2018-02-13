using System;
using UnityEngine;

namespace Unity.Properties
{
    public class Property<TContainer, TValue> : IProperty<TContainer, TValue>
        where TContainer : IPropertyContainer
    {
        public delegate void SetValueMethod(ref TContainer container, TValue value);
        public delegate TValue GetValueMethod(ref TContainer container);

        private readonly GetValueMethod m_GetValue;
        private readonly SetValueMethod m_SetValue;

        public string Name { get; }
        public Type PropertyType => typeof(TValue);
        public virtual bool IsReadOnly => m_SetValue == null;

        private Property(string name)
        {
            Name = name;
        }
        
        public Property(string name, GetValueMethod getValue, SetValueMethod setValue) 
            : this(name)
        {
            m_GetValue = getValue;
            m_SetValue = setValue;
        }
        
        public int GetVersion(IPropertyContainer container)
        {
            return container.VersionStorage?.GetVersion(this, ref container) ?? 0;
        }
        
        public int GetVersion(ref TContainer container)
        {
            return container.VersionStorage?.GetVersion(this, ref container) ?? 0;
        }

        public object GetObjectValue(ref IPropertyContainer container)
        {
            var typed = (TContainer) container;
            var result = GetValue(ref typed);
            container = typed;
            return result;
        }

        public void SetObjectValue(ref IPropertyContainer container, object value)
        {
            var typed = (TContainer) container;

            if (value is IConvertible)
            {
                SetValue(ref typed, (TValue) Convert.ChangeType(value, typeof(TValue)));
            }
            else
            {
                SetValue(ref typed, (TValue) value);
            }
            
            container = typed;
        }

        public virtual void Accept(IPropertyContainer container, IPropertyVisitor visitor)
        {
            Debug.Assert(container is TContainer, $"container of type {container.GetType()} and not type {typeof(TContainer)} on property {Name}");
            var t = (TContainer)container;
            Accept(ref t, visitor);
        }

        public TValue GetValue(ref IPropertyContainer container)
        {
            var typed = (TContainer) container;
            var result = GetValue(ref typed);
            container = typed;
            return result;
        }

        public void SetValue(ref IPropertyContainer container, TValue value)
        {
            var typed = (TContainer) container;
            SetValue(ref typed, value);
            container = typed;
        }

        public virtual TValue GetValue(ref TContainer container)
        {
            return m_GetValue(ref container);
        }

        public virtual TValue GetValue(TContainer container)
        {
            return m_GetValue(ref container);
        }

        public virtual void SetValue(ref TContainer container, TValue value)
        {
            if (Equals(container, value))
            {
                return;
            }
            Debug.Assert(m_SetValue != null, $"{typeof(TContainer)}.{Name} Property cannot be set");
            m_SetValue(ref container, value);
            container.VersionStorage?.IncrementVersion(this, ref container);
        }

        public virtual void SetValue(TContainer container, TValue value)
        {
            if (Equals(container, value))
            {
                return;
            }
            
            m_SetValue(ref container, value);
            container.VersionStorage?.IncrementVersion(this, ref container);
        }

        private bool Equals(TContainer container, TValue value)
        {
            var v = m_GetValue(ref container);

            if (null == v && null == value)
            {
                return true;
            }

            return null != v && v.Equals(value);
        }

        public virtual void Accept(ref TContainer container, IPropertyVisitor visitor)
        {
            // Have a single GetValue call per property visit
            // Get value is a user driven operation. 
            // We don't know performance impact or side effects
            var value = GetValue(ref container);
            
            // Delegate the Visit implementaton to the user
            if (TryUserAccept(ref container, visitor, value))
            {
                // User has handled the visit; early exit
                return;
            }
            
            // Default visit implementation
            visitor.Visit(ref container, new VisitContext<TValue> {Property = this, Value = value, Index = -1});
        }

        /// <summary>
        /// Checks for user implemntations of IPropertyVisitor and IPropertyVisitorValidaton and returns true if the visit was handled
        /// </summary>
        /// <param name="container"></param>
        /// <param name="visitor"></param>
        /// <returns>True if the default Accept method should be skipped</returns>
        protected bool TryUserAccept(ref TContainer container, IPropertyVisitor visitor, TValue value)
        {
            var typedValidation = visitor as IPropertyVisitorValidation<TValue>;

            if (null != typedValidation && !typedValidation.ShouldVisit(ref container, new VisitContext<TValue> {Property = this, Value = value, Index = -1}))
            {
                return true;
            }

            var validation = visitor as IPropertyVisitorValidation;
            
            if (null != validation && !validation.ShouldVisit(ref container, new VisitContext<TValue> {Property = this, Value = value, Index = -1}))
            {
                return true;
            }

            var typedVisitor = visitor as IPropertyVisitor<TValue>;

            if (null == typedVisitor)
            {
                return false;
            }
            
            return typedVisitor.Visit(ref container, new VisitContext<TValue> {Property = this, Value = value, Index = -1});
        }
    }
}