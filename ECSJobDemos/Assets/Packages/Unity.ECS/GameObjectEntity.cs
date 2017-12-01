using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;

namespace UnityEngine.ECS
{
    
    //@TODO: This should be fully implemented in C++ for efficiency
    [RequireComponent(typeof(GameObjectEntity))]
    public abstract class ComponentDataWrapperBase : MonoBehaviour
    {
        abstract internal ComponentType GetComponentType(EntityManager manager);
        abstract internal void UpdateComponentData(EntityManager manager, Entity entity);
    }

    //@TODO: This should be fully implemented in C++ for efficiency
    public class ComponentDataWrapper<T> : ComponentDataWrapperBase where T : struct, IComponentData
    {
        [SerializeField]
        T m_SerializedData;

        public T Value
        {
            get
            { 
                return m_SerializedData;
            }
            set
            {
                m_SerializedData = value;
            }
        }


        internal override ComponentType GetComponentType(EntityManager manager)
        {
            return ComponentType.Create<T>();
        }

        internal override void UpdateComponentData(EntityManager manager, Entity entity)
        {
            manager.SetComponent(entity, m_SerializedData);
        }
    }

    //@TODO: This should be fully implemented in C++ for efficiency
    public class SharedComponentDataWrapper<T> : ComponentDataWrapperBase where T : struct, ISharedComponentData
    {
        [SerializeField]
        T m_SerializedData;

        public T Value
        {
            get
            {
                return m_SerializedData;
            }
            set
            {
                m_SerializedData = value;
            }
        }


        internal override ComponentType GetComponentType(EntityManager manager)
        {
            return manager.CreateSharedComponentType(m_SerializedData);
        }

        internal override void UpdateComponentData(EntityManager manager, Entity entity)
        {
        }
    }
    
    [DisallowMultipleComponent]
    public class GameObjectEntity : MonoBehaviour
    {
        EntityManager m_EntityManager;
        Entity m_Entity;

        public Entity Entity { get { return m_Entity; } }

        static public Entity AddToEntityManager(EntityManager entityManager, GameObject gameObject)
        {
            int t;
            var gameObjectEntityComponent = gameObject.GetComponent<GameObjectEntity>();
            var components = gameObject.GetComponents<Component>();
            var componentCount = (gameObjectEntityComponent == null) ? (components.Length) : (components.Length - 1);            
            ComponentType[] types = new ComponentType[componentCount];

            t = 0;
            for (int i = 0; i != components.Length; i++)
            {
                var com = components[i];
                var componentData = com as ComponentDataWrapperBase;

                if (componentData != null)
                    types[t++] = componentData.GetComponentType(entityManager);
                else if (!(com is GameObjectEntity))
                    types[t++] = com.GetType();
            }

            var archetype = entityManager.CreateArchetype(types);
            var entity = entityManager.CreateEntity(archetype);
            t = 0;
            for (int i = 0; i != components.Length; i++)
            {
                var com = components[i];
                var componentDataWrapper = com as ComponentDataWrapperBase;

                if (componentDataWrapper != null)
                {
                    componentDataWrapper.UpdateComponentData(entityManager, entity);
                    t++;
                }
                else if (!(com is GameObjectEntity))
                {
                    entityManager.SetComponentObject(entity, types[t], com);
                    t++;
                }
            }
            return entity;
        }

        public void OnEnable()
        {
            m_EntityManager = World.Active.GetOrCreateManager<EntityManager>();
            m_Entity = AddToEntityManager(m_EntityManager, gameObject);
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
