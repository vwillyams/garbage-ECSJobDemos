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
    [RequireComponent(typeof(GameObjectEntity))]
    public abstract class ComponentDataWrapperBase : ScriptBehaviour
    {
        abstract internal Type GetIComponentDataType();
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


		internal override Type GetIComponentDataType()
		{
			return typeof(T);
		}

        internal override void UpdateComponentData(EntityManager manager, Entity entity)
        {
            manager.SetComponent(entity, m_SerializedData);
        }

    }
}