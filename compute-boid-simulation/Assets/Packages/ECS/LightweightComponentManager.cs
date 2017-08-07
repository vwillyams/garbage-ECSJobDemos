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

		void AddElements (GameObject srcGameObject, NativeSlice<int> outComponentIndices);
		void RemoveElement (NativeArray<int> elements);

		void CollectSupportedTupleSets (Type[] supportedTypes, HashSet<TupleSystem> tuples);

		List<TupleSystem.RegisteredTuple> GetRegisteredTuples ();
    }

    //@TODO: This should be fully implemented in C++ for efficiency
    public class LightweightComponentManager<T> : ScriptBehaviourManager, ILightweightComponentManager where T : struct, IComponentData
    {
    	internal NativeFreeList<T>                          m_Data;
    //	internal NativeList<JobHandle>                      m_Readers;
        JobHandle                                           m_Writer;

    	internal List<TupleSystem.RegisteredTuple>         	m_RegisteredTuples;
		internal LightweightGameObjectManager				m_GameObjectManager;

        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            m_Data = new NativeFreeList<T>(Allocator.Persistent);
			m_Data.Capacity = capacity;
            m_RegisteredTuples = new List<TupleSystem.RegisteredTuple>();
        }

    	protected override void OnDestroyManager()
    	{
    		base.OnDestroyManager();

			CompleteForWriting ();
    		m_Data.Dispose();
			m_RegisteredTuples = null;
			m_GameObjectManager = null;
    	}

		public void AddElements (GameObject sourceGameObject, NativeSlice<int> outComponentIndices)
    	{
			CompleteForWriting ();

			var value = sourceGameObject.GetComponent<ComponentDataWrapper<T> > ().Value;

    		int baseIndex = m_Data.Length;
			for (int i = 0; i != outComponentIndices.Length; i++)
				outComponentIndices[i] = m_Data.Add (value);
    	}

		public int AddElement(T value)
		{
			CompleteForWriting ();
			return m_Data.Add (value);
		}

    	public void CollectSupportedTupleSets(Type[] requiredComponentTypes, HashSet<TupleSystem> tuples)
    	{
    		foreach (var tuple in m_RegisteredTuples)
    		{
    			if (requiredComponentTypes == null || tuple.tupleSystem.IsLightWeightTupleSupported (requiredComponentTypes))
    				tuples.Add (tuple.tupleSystem);
    		}
    	}

		public void RemoveElement (NativeArray<int> elements)
    	{
			CompleteForWriting ();

			for (int i = 0; i < elements.Length; i++)
				m_Data.Remove (elements[i]);
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
			#if ENABLE_NATIVE_ARRAY_CHECKS
			if (!JobHandle.CheckFenceIsDependencyOrDidSyncFence(m_Writer, handle))
			{
				Debug.LogError("AddDependency is required to depend on any previous jobs (Use GetDependency())");
			}
			#endif

    		m_Writer = handle;
    	}

    	public void AddReadDependency(JobHandle handle)
    	{
			AddWriteDependency (handle);
    	}
		public List<TupleSystem.RegisteredTuple> GetRegisteredTuples ()
		{
			return m_RegisteredTuples;
		}
    }
}