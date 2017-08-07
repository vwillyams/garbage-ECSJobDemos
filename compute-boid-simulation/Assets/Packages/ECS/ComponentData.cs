using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

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
		LightweightGameObjectManager   m_GameObjectManager;

        [SerializeField]
        T m_SerializedData;

		LightweightGameObject GetLightWeightGameObject()
		{
			return new LightweightGameObject(0, gameObject.GetInstanceID ());
		}

    	public T Value
    	{
    		get
    		{ 
				if (m_GameObjectManager != null)
    			{
					var go = GetLightWeightGameObject();
					if (m_GameObjectManager.HasComponent<T> (go))
					{
						return m_GameObjectManager.GetComponent<T> (go);
					}
    			}
    			
				return m_SerializedData;
    		}
    		set
    		{
				if (m_GameObjectManager != null)
				{
					var go = GetLightWeightGameObject();
					if (m_GameObjectManager.HasComponent<T> (go))
					{
						m_GameObjectManager.SetComponent<T> (go, value);
						return;
					}
				}

    			m_SerializedData = value;
    		}
    	}

    	public void OnAfterDeserialize ()
    	{
			if (m_GameObjectManager != null)
    		{
				var go = GetLightWeightGameObject();
				if (m_GameObjectManager.HasComponent<T> (go))
				{
					m_GameObjectManager.SetComponent<T> (go, m_SerializedData);
				}
    		}
    	}

    	public void OnBeforeSerialize ()
    	{
			if (m_GameObjectManager != null)
			{			
				var go = GetLightWeightGameObject();
				if (m_GameObjectManager.HasComponent<T> (go))
				{
					m_SerializedData = m_GameObjectManager.GetComponent<T> (GetLightWeightGameObject ());
				}
    		}
    	}

		internal override Type GetIComponentDataType()
		{
			return typeof(T);
		}

        override protected void OnEnable()
        {
			if (m_GameObjectManager == null)
				m_GameObjectManager = DependencyManager.GetBehaviourManager (typeof(LightweightGameObjectManager)) as LightweightGameObjectManager;

			m_GameObjectManager.AddComponent(GetLightWeightGameObject(), m_SerializedData);
        }

    	override protected void OnDisable()
    	{
			m_GameObjectManager.RemoveComponent<T>(GetLightWeightGameObject());
			m_GameObjectManager = null;
    	}
    }
}