using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;

namespace UnityEngine.ECS
{
    public interface IComponentData
    { 
    }

    public interface ISharedComponentData
    {

    }

    //@TODO: This should be fully implemented in C++ for efficiency
    [RequireComponent(typeof(GameObjectEntity))]
    public abstract class ComponentDataWrapperBase : ScriptBehaviour
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
}