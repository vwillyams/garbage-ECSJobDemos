using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;

namespace UnityEngine.ECS
{
    public interface IComponentData
    { 
    }


    //@TODO: This should be fully implemented in C++ for efficiency
    public abstract class ComponentDataWrapperBase : ScriptBehaviour
    { 
		abstract internal Type GetIComponentDataType ();
    }

    //@TODO: This should be fully implemented in C++ for efficiency
    public class ComponentDataWrapper<T> : ComponentDataWrapperBase, UnityEngine.ISerializationCallbackReceiver where T : struct, IComponentData
    {
		EntityManager   m_GameObjectManager;
		Entity			m_Entity;

        [SerializeField]
        T m_SerializedData;

    	public T Value
    	{
    		get
    		{ 
				if (m_GameObjectManager != null && m_GameObjectManager.HasComponent<T> (m_Entity))
    			{
					return m_GameObjectManager.GetComponent<T> (m_Entity);
    			}
    			
				return m_SerializedData;
    		}
    		set
    		{
				m_SerializedData = value;
				if (m_GameObjectManager != null && m_GameObjectManager.HasComponent<T> (m_Entity))
				{
					m_GameObjectManager.SetComponent<T> (m_Entity, value);
					return;
				}

    		}
    	}

    	public void OnAfterDeserialize ()
    	{
			if (m_GameObjectManager != null && m_GameObjectManager.HasComponent<T> (m_Entity))
    		{
				m_GameObjectManager.SetComponent<T> (m_Entity, m_SerializedData);
    		}
    	}

    	public void OnBeforeSerialize ()
    	{
			if (m_GameObjectManager != null && m_GameObjectManager.HasComponent<T> (m_Entity))
			{			
				m_SerializedData = m_GameObjectManager.GetComponent<T> (m_Entity);
    		}
    	}

		internal override Type GetIComponentDataType()
		{
			return typeof(T);
		}

        override protected void OnEnable()
        {
#if !ECS_ENTITY_CLASS && !ECS_ENTITY_TABLE
			m_GameObjectManager = DependencyManager.GetBehaviourManager (typeof(EntityManager)) as EntityManager;
			m_Entity = new Entity (0, gameObject.GetInstanceID ());

			m_GameObjectManager.AddComponent(m_Entity, m_SerializedData);
#endif
        }

    	override protected void OnDisable()
    	{
#if !ECS_ENTITY_CLASS && !ECS_ENTITY_TABLE
			if (m_GameObjectManager != null && m_GameObjectManager.HasComponent<T>(m_Entity))
				m_GameObjectManager.RemoveComponent<T>(m_Entity);

			m_GameObjectManager = null;
			m_Entity = new Entity ();
#endif
    	}
    }
}