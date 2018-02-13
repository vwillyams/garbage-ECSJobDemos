using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Properties
{
    public class PropertyBag : IPropertyBag
    {
        private readonly List<IProperty> m_Properties;
        private readonly Dictionary<string, IProperty> m_Map;

        public int PropertyCount => m_Properties.Count;
        
        public IEnumerable<IProperty> Properties => m_Properties;

        public PropertyBag()
        {
            m_Properties = new List<IProperty>();
            m_Map = new Dictionary<string, IProperty>();
        }

        public PropertyBag(params IProperty[] properties)
        {
            m_Properties = new List<IProperty>(properties);
            m_Map = new Dictionary<string, IProperty>(properties.Length);
            foreach (var n in properties)
            {
                Assert.IsFalse(m_Map.ContainsKey(n.Name), $"PropertyBag already contains a property named {n.Name}");
                m_Map[n.Name] = n;
            }
        }
        
        IPropertyContainer GetInnerContainer(IPropertyContainer container, IProperty p, int index = -1)
        {
            IPropertyContainer c = null;

            if (container != null && p != null)
            {
                if (p is IListProperty)
                {
                    var listProp = p as IListProperty;
                    var value = listProp.GetObjectValueAtIndex(container, index);
                    if (value is IPropertyContainer)
                    {
                        c = value as IPropertyContainer;
                    }
                }
                else if (typeof(IPropertyContainer).IsAssignableFrom(p.PropertyType))
                    c = (IPropertyContainer)p.GetObjectValue(ref container) as IPropertyContainer;

                else if (c == null)
                {
                    ISubtreeContainer prop = p as ISubtreeContainer;
                    if (prop != null)
                    {
                        c = prop.GetSubtreeContainer(container, p);
                    }
                }
            }
            return c;
        }

        public void AddProperty(IProperty property)
        {
            Assert.IsNotNull(property);
            Assert.IsFalse(m_Map.ContainsKey(property.Name));
            
            m_Properties.Add(property);
            m_Map[property.Name] = property;
        }

        public void RemoveProperty(IProperty property)
        {
            m_Properties.Remove(property);
            m_Map.Remove(property.Name);
        }

        public void Clear()
        {
            m_Properties.Clear();
            m_Map.Clear();
        }

        public IEnumerable<PropertyTreeNode> Traverse(IPropertyContainer container, bool includeChildren)
        {
            // TODO: optimize or remove this method. Wrong on so many levels.

            var list = new List<PropertyTreeNode>();
            foreach (var p in m_Properties)
            {
                list.Add(new PropertyTreeNode(container, p));
                
                if (!includeChildren) continue;

                var child = GetInnerContainer(container, p);
                if (child == null) continue;

                list.AddRange(child.PropertyBag.Traverse(child, true));
            }
            return list;
        }

        public IProperty FindProperty(string name)
        {
            IProperty prop;
            return m_Map.TryGetValue(name, out prop) ? prop : null;
        }

        public PropertyTreeNode ResolveProperty(IPropertyContainer container, PropertyPath path)
        {
            var c = container;
            for (var i = 0; i < path.Parts.Length - 1; ++i)
            {
                if (c?.PropertyBag != null)
                {
                    var part = path.Parts[i];
                    int index = -1;
                    var oBracket = part.IndexOf('[');
                    if (oBracket >= 0)
                    {
                        var eBracket = part.IndexOf(']');
                        var length = eBracket - (oBracket + 1);
                        if (!int.TryParse(part.Substring(oBracket + 1, length), out index))
                        {
                            index = -1;
                        }
                    }

                    var propertyName = index < 0 ? part : part.Substring(0, oBracket);
                    c = GetInnerContainer(c, c.PropertyBag.FindProperty(propertyName), index);
                }
            }
            IProperty property = null;
            if (c?.PropertyBag != null)
            {
                property = c.PropertyBag.FindProperty(path.Parts[path.Parts.Length - 1]);
            }

            return new PropertyTreeNode(c, property);
        }
        
        public bool Visit<TContainer>(ref TContainer container, IPropertyVisitor visitor) 
            where TContainer : IPropertyContainer
        {
            for (var i=0; i<m_Properties.Count; i++)
            {
                // Try to avoid boxing by casing directly to the typed container
                var typed = m_Properties[i] as ITypedContainerProperty<TContainer>;
                if (null != typed)
                {
                    typed.Accept(ref container, visitor);
                }
                else
                {
                    // Downcast to IPropertyContainer and let the property cast to the correct type
                    m_Properties[i].Accept(container, visitor);
                }
            }

            return true;
        }
    }
}