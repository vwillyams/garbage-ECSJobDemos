using UnityEngine;
using UnityEngine.Collections;
using UnityEngine.Jobs;
using System;
using System.Collections.Generic;
using UnityEngine.Assertions;
using System.Linq;
using System.Reflection;

namespace ECS
{
    public interface IComponentData
    { 
    }


    //@TODO: This should be fully implemented in C++ for efficiency
    public class ComponentDataWrapperBase : ScriptBehaviour
    { 
    	[NonSerialized]
        public int         m_Index = -1;
    	[NonSerialized]
        public Type        m_LightWeightType;

    	protected override void OnCreate ()
    	{
    		base.OnCreate ();
    	}
    }

    //@TODO: This should be fully implemented in C++ for efficiency
    public class ComponentDataWrapper<T> : ComponentDataWrapperBase, UnityEngine.ISerializationCallbackReceiver where T : struct, IComponentData
    {
        [SerializeField]
        T m_SerializedData;

    	public T Value
    	{
    		get
    		{ 
    			if (m_Index != -1)
    			{
    				m_Manager.CompleteForReading ();
    				return m_Manager.m_Data [m_Index];
    			}
    			else
    				return m_SerializedData;
    		}
    		set
    		{
    			if (m_Index != -1)
    			{
    				m_Manager.CompleteForWriting ();
    				m_Manager.m_Data [m_Index] = value;
    			}
    			else
    				m_SerializedData = value;
    		}
    	}

    	public void OnAfterDeserialize ()
    	{
    		if (m_Index != -1)
    		{
    			m_Manager.CompleteForWriting ();
    			m_Manager.m_Data[m_Index] = m_SerializedData;
    		}
    	}

    	public void OnBeforeSerialize ()
    	{
    		if (m_Index != -1)
    		{
    			m_Manager.CompleteForReading ();

    			m_SerializedData = m_Manager.m_Data [m_Index];
    		}
    	}

    	static LightweightComponentManager<T> m_Manager;

        override protected void OnEnable()
        {
    		//@TODO: Why not constructor?
    		m_LightWeightType = typeof(T);

    		if (m_Manager == null)
    			m_Manager = DependencyManager.GetBehaviourManager (typeof(LightweightComponentManager<T>)) as LightweightComponentManager<T>;
            m_Manager.AddElement(m_SerializedData, this);
        }

    	override protected void OnDisable()
    	{
    		m_Manager.RemoveElement(this);
    	}
    }
}