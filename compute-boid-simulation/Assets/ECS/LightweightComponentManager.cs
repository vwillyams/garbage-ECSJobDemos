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
    public interface ILightweightComponentManager
    {
    	JobHandle GetReadDependency ();
    	JobHandle GetWriteDependency ();

    	void CompleteWriteDependency ();
    	void CompleteReadDependency ();

    	void AddWriteDependency (JobHandle handle);
    	void AddReadDependency (JobHandle handle);

    	int AddElements (ILightweightComponentManager src, int srcIndex, NativeArray<LightweightGameObject> gameObjects);
    	void RemoveElements (NativeArray<int> elements);

    	void CollectSupportedTupleSets (Type[] supportedTypes, HashSet<TupleSystem> tuples);
    }

    //@TODO: This should be fully implemented in C++ for efficiency
    public class LightweightComponentManager<T> : ScriptBehaviourManager, ILightweightComponentManager where T : struct, IComponentData
    {
    	internal NativeList<T>                              m_Data;
    	internal NativeList<LightweightGameObject>          m_LightweightGameObject;
        internal List<ComponentDataWrapperBase>      m_Components;
    //	internal NativeList<JobHandle>                      m_Readers;
        JobHandle                                           m_Writer;


    	internal List<TupleSystem.RegisteredTuple>         m_RegisteredTuples;

        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

    		m_Components = new List<ComponentDataWrapperBase> ();
            m_Data = new NativeList<T>(capacity, Allocator.Persistent);
    		m_LightweightGameObject = new NativeList<LightweightGameObject> (capacity, Allocator.Persistent);
            m_RegisteredTuples = new List<TupleSystem.RegisteredTuple>();
        }

    	protected override void OnDestroyManager()
    	{
    		base.OnDestroyManager();
    		m_Data.Dispose();
    		m_LightweightGameObject.Dispose ();
    	}

    	public int AddElements (ILightweightComponentManager src, int srcIndex, NativeArray<LightweightGameObject> gameObjects)
    	{
    		var castedSrc = src as LightweightComponentManager<T>;

    		int baseIndex = m_Data.Length;
    		for (int i = 0; i != gameObjects.Length; i++)
    		{
    			m_Data.Add (castedSrc.m_Data[srcIndex]);
    			m_Components.Add (null);
    			m_LightweightGameObject.Add (gameObjects[i]);
    		}
    		return baseIndex;
    	}

    	public void CollectSupportedTupleSets(Type[] requiredComponentTypes, HashSet<TupleSystem> tuples)
    	{
    		foreach (var tuple in m_RegisteredTuples)
    		{
    			if (requiredComponentTypes == null || tuple.tupleSystem.IsLightWeightTupleSupported (requiredComponentTypes))
    				tuples.Add (tuple.tupleSystem);
    		}
    	}

    	public void RemoveElements (NativeArray<int> elements)
    	{
    		
    	}

    	internal void AddElement(T serializedData, ComponentDataWrapperBase com)
        {
    		CompleteForWriting ();

            int index = m_Data.Length;
            m_Data.Add(serializedData);
    		m_Components.Add(com);
    		m_LightweightGameObject.Add (new LightweightGameObject());
            com.m_Index = index;

    		foreach (var tuple in m_RegisteredTuples)
    			tuple.tupleSystem.AddTupleIfSupported(com.gameObject);
        }
    		
    	internal void RemoveElement(ComponentDataWrapperBase com)
    	{
    		CompleteForWriting ();

            var lastEntity = m_Components[m_Components.Count - 1];
    		if (lastEntity != null)
    		{
    			lastEntity.m_Index = com.m_Index;
    		}

    		foreach (var tuple in m_RegisteredTuples)
    		{
    			tuple.tupleSystem.RemoveSwapBackLightWeightComponent (tuple.tupleSystemIndex, com.m_Index);
    		}

			if (m_Data.IsCreated)
	    		m_Data.RemoveAtSwapBack(com.m_Index);
    		m_Components.RemoveAtSwapBack(com.m_Index);
			if (m_LightweightGameObject.IsCreated)
	    		m_LightweightGameObject.RemoveAtSwapBack (com.m_Index);

            com.m_Index = -1;
        }

    	//@TODO: Proper support for read / write dependencies

    	public JobHandle GetReadDependency()
    	{
    		return m_Writer;
    	}

    	public JobHandle GetWriteDependency()
    	{
    		return m_Writer;
    	}

    	public void CompleteWriteDependency()
    	{
    		m_Writer.Complete();
    	}

    	public void CompleteReadDependency()
    	{
    		m_Writer.Complete();
    	}

    	public void CompleteForWriting()
    	{
    		CompleteWriteDependency ();
    		CompleteReadDependency ();
    	}

    	public void CompleteForReading()
    	{
    		CompleteWriteDependency ();
    	}

    	public void AddWriteDependency(JobHandle handle)
    	{
    		//@TODO: JobDebugger check that dependency depends on all previous?
    		m_Writer = handle;
    	}

    	public void AddReadDependency(JobHandle handle)
    	{
    		m_Writer = handle;
    	}
    }
}