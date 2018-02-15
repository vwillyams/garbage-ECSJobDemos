using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.Properties.ECS
{
    public unsafe struct StructProxy : IPropertyContainer
    {
        public IVersionStorage VersionStorage => PassthroughVersionStorage.Instance;
        public IPropertyBag PropertyBag { get; set; }

        public Type ComponentType;
        public byte* ComponentData;
        
        private List<StructProxy> m_Children;

        public TValue GetValueAtOffset<TValue>(int offset) where TValue : struct
        {
            TValue result;
            UnsafeUtility.CopyPtrToStructure(ComponentData + offset, out result);
            return result;
        }
        
        private static Dictionary<Type, PropertyBag> s_PropertyBagCache = new Dictionary<Type, PropertyBag>();

        public void Setup(ComponentType cType, EntityManager manager, Entity entity)
        {
            var data = (byte*) manager.GetComponentDataRaw(entity, cType.typeIndex);
            var type = TypeManager.GetType(cType.typeIndex);
            
            SetupChild(type, data);
        }

        private void SetupChild(Type componentType, byte* data)
        {
            ComponentType = componentType;
            ComponentData = data;
            PropertyBag = CreateTypeTree(ComponentType);

            foreach (var p in PropertyBag.Properties)
            {
                if (p is NestedProxyProperty)
                {
                    if (m_Children == null)
                    {
                        m_Children = new List<StructProxy>();
                    }
                    var nestedProperty = p as NestedProxyProperty;
                    var child = new StructProxy();
                    child.SetupChild(nestedProperty.ComponentType, data + nestedProperty.FieldOffset);
                    m_Children.Add(child);
                }
            }
        }

        public static PropertyBag CreateTypeTree(Type componentType)
        {
            PropertyBag result;
            if (s_PropertyBagCache.TryGetValue(componentType, out result))
            {
                return result;
            }

            var childIndex = 0;
            var properties = new List<IProperty> {ComponentIdProperty};
            foreach (var field in componentType.GetFields())
            {
                // we only support public struct fields in this model
                if (field.IsPublic && field.FieldType.IsValueType)
                {
                    IProperty property;
                    
                    if (typeof(IComponentData).IsAssignableFrom(field.FieldType))
                    {
                        // composite
                        property = new NestedProxyProperty(field, childIndex++);
                        CreateTypeTree(field.FieldType); // pre-warm
                    }
                    else
                    {
                        // assumption: use an IOptimizedVisitor everywhere
                        if (OptimizedVisitor.Supports(field.FieldType))
                        {
                            // primitive
                            // TODO: this is a hack until we have code gen
                            var propertyType = typeof(PrimitiveProperty<>).MakeGenericType(field.FieldType);
                            property = (IProperty) Activator.CreateInstance(propertyType, field);
                        }
                        else
                        {
                            if (field.FieldType.IsPrimitive)
                            {
                                throw new NotSupportedException($"Primitive field type {field.FieldType} is not supported");
                            }
                            // composite
                            property = new NestedProxyProperty(field, childIndex++);
                            CreateTypeTree(field.FieldType); // pre-warm
                        }
                    }
                    
                    properties.Add(property);
                }
            }
            result = new PropertyBag(properties);
            s_PropertyBagCache[componentType] = result;
            return result;
        }
        
        private class TypeIdProperty : StructProperty<StructProxy, string>
        {
            public TypeIdProperty(GetValueMethod getValue) : base("$TypeId", getValue, null)
            {
            }
        }
        
        private static IProperty ComponentIdProperty = new TypeIdProperty(
            (ref StructProxy c) => c.ComponentType.FullName);

        private class NestedProxyProperty : StructMutableContainerProperty<StructProxy, StructProxy>
        {
            public int FieldOffset { get; }
            public Type ComponentType { get; }
            
            private int ChildIndex { get; }
            
            public NestedProxyProperty(FieldInfo field, int childIndex) 
                : base(field.Name, null, null, null)
            {
                FieldOffset = UnsafeUtility.GetFieldOffset(field);
                ComponentType = field.FieldType;
                ChildIndex = childIndex;
                RefAccess = GetChildRef;
            }

            private void GetChildRef(ref StructProxy container, RefVisitMethod refVisitMethod, IPropertyVisitor visitor)
            {
                var child = container.m_Children[ChildIndex]; // no way to get a ref since this is from a list
                refVisitMethod(ref child, visitor);
                container.m_Children[ChildIndex] = child;
            }

            public override StructProxy GetValue(ref StructProxy container)
            {
                return container.m_Children[ChildIndex];
            }
        }
        
        private class PrimitiveProperty<TValue> : StructProperty<StructProxy, TValue>
            where TValue : struct
        {
            private int FieldOffset { get; }

            public PrimitiveProperty(FieldInfo field) : base(field.Name, null, null)
            {
                FieldOffset = UnsafeUtility.GetFieldOffset(field);
            }
            
            public override TValue GetValue(ref StructProxy container)
            {
                return container.GetValueAtOffset<TValue>(FieldOffset);
            }
        }
    }
}