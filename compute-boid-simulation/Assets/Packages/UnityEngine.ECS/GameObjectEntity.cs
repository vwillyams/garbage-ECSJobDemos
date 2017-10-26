using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;

namespace UnityEngine.ECS
{
    [DisallowMultipleComponent]
    public class GameObjectEntity : MonoBehaviour
    {
        EntityManager m_EntityManager;
        Entity m_Entity;

        public Entity Entity { get { return m_Entity; } }

        public void OnEnable()
        {
            int t;
            var components = GetComponents<Component>();
            ComponentType[] types = new ComponentType[components.Length - 1];

            m_EntityManager = DependencyManager.GetBehaviourManager(typeof(EntityManager)) as EntityManager;

            t = 0;
            for (int i = 0; i != components.Length;i++)
            {
                var com = components[i];
                var componentData = com as ComponentDataWrapperBase;

                if (componentData != null)
                    types[t++] = componentData.GetComponentType(m_EntityManager);
                else if (com is GameObjectEntity)
                    ;
                else
                    types[t++] = com.GetType();
            }

            var archetype = m_EntityManager.CreateArchetype(types);
            m_Entity = m_EntityManager.CreateEntity(archetype);
            t = 0;
            for (int i = 0; i != components.Length; i++)
            {
                var com = components[i];
                var componentDataWrapper = com as ComponentDataWrapperBase;

                if (componentDataWrapper != null)
                {
                    componentDataWrapper.UpdateComponentData(m_EntityManager, m_Entity);
                    t++;
                }
                else if (com is GameObjectEntity)
                    ;
                else
                {
                    m_EntityManager.SetComponentObject(m_Entity, types[t], com);
                    t++;
                }
                    
            }
        }

        public void OnDisable()
        {
            if (m_EntityManager != null && m_EntityManager.IsCreated && m_EntityManager.Exists(m_Entity))
                m_EntityManager.DestroyEntity(m_Entity);

            m_EntityManager = null;
            m_Entity = new Entity();
        }
    }
}
