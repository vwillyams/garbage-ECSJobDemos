using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ECS;

namespace Unity.Properties.ECS
{
    /// <summary>
    /// Reusable container to iterate on Entity instances.
    /// </summary>
    public class EntityContainer : IPropertyContainer
    {
        private List<StructProxy> m_Proxies;
        
        public void Setup(EntityManager manager, Entity entity)
        {
            var types = manager.GetComponentTypes(entity);
            try
            {
                bool different = m_Proxies == null || m_Proxies.Count != types.Length;
                if (!different)
                {
                    for (int i = 0; i < m_Proxies.Count; ++i)
                    {
                        if (m_Proxies[i].ComponentType != TypeManager.GetType(types[i].typeIndex))
                        {
                            different = true;
                            break;
                        }
                    }
                }
                if (different)
                {
                    var list = new List<StructProxy>(types.Length);
                    foreach (var t in types)
                    {
                        var proxy = CreateProxy(manager, entity, t);
                        list.Add(proxy);
                    }
    
                    m_Proxies = list;
                }
            }
            finally
            {
                types.Dispose();
            }
        }

        private static StructProxy CreateProxy(EntityManager manager, Entity entity, ComponentType t)
        {
            var proxy = new StructProxy();
            proxy.Setup(t, manager, entity);
            return proxy;
        }

        private static IListProperty ComponentsProperty =
            new MutableContainerListProperty<EntityContainer, List<StructProxy>, StructProxy>(
                "Components",
                c => c.m_Proxies,
                null);
        
        public IVersionStorage VersionStorage => PassthroughVersionStorage.Instance;
        
        private static PropertyBag s_PropertyBag = new PropertyBag(ComponentsProperty);
        public IPropertyBag PropertyBag => s_PropertyBag;
    }

}
