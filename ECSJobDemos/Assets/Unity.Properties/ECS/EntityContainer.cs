using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.ECS;
using UnityEngine.Profiling;

namespace Unity.Properties.ECS
{
    /// <summary>
    /// Reusable container to iterate on Entity instances.
    /// </summary>
    public unsafe class EntityContainer : StructProxyList, IPropertyContainer, IDisposable
    {
        private EntityManager m_Manager;
        private Entity m_Entity;
        private NativeArray<ComponentType> m_Types;
        
        public void Setup(EntityManager manager, Entity entity)
        {
            Dispose();
            m_Manager = manager;
            m_Entity = entity;
            Profiler.BeginSample("manager.GetComponentTypes");
            m_Types = manager.GetComponentTypes(entity);
            Profiler.EndSample();
        }
        
        public void Dispose()
        {
            if (m_Types.IsCreated)
                m_Types.Dispose();
            
            m_Manager = null;
            m_Entity = Entity.Null;
        }

        private class ReadOnlyComponentsProperty : MutableContainerListProperty<EntityContainer, IList<StructProxy>, StructProxy>
        {
            public ReadOnlyComponentsProperty(string name, GetValueMethod getValue) : base(name, getValue, null, null)
            {
            }

            public override void Accept(EntityContainer container, IPropertyVisitor visitor)
            {
                var value = GetValue(container);
           
                var listContext = new ListContext<IList<StructProxy>> {Property = this, Value = value, Index = -1, Count = value.Count};
    
                if (visitor.BeginList(ref container, listContext))
                {
                    var count = Count(container);
                    for (var i=0; i<count; i++)
                    {
                        var item = GetValueAtIndex(container, i);
                        var context = new SubtreeContext<StructProxy>
                        {
                            Property = this,
                            Value = item,
                            Index = i
                        };
                    
                        if (visitor.BeginContainer(ref container, context))
                        {
                            item.PropertyBag.VisitStruct(ref item, visitor);
                        }
                        visitor.EndContainer(ref container, context);
                    }
                }
                visitor.EndList(ref container, listContext);
            }
        }

        private static IListProperty ComponentsProperty = new ReadOnlyComponentsProperty(
                "Components",
                c => c);
        
        public IVersionStorage VersionStorage => PassthroughVersionStorage.Instance;
        
        private static PropertyBag s_PropertyBag = new PropertyBag(ComponentsProperty);
        public IPropertyBag PropertyBag => s_PropertyBag;
        
        public override int Count => m_Types.IsCreated ? m_Types.Length : 0;
        
        public override StructProxy this[int index]
        {
            get
            {
                var propertyType = TypeManager.GetType(m_Types[index].typeIndex);
                var propertyBag = TypeInformation.GetOrCreate(propertyType);
                Profiler.BeginSample("GetComponentDataRaw");
                var data = (byte*)m_Manager.GetComponentDataRaw(m_Entity, m_Types[index].typeIndex);
                Profiler.EndSample();
                var p = new StructProxy()
                {
                    bag = propertyBag,
                    data = data,
                    type = propertyType
                };
                return p;
            }
            set { throw new NotImplementedException(); }
        }
    }

}
