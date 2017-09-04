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
    public interface IComponentDataManager
    {
    	JobHandle GetReadDependency ();
    	JobHandle GetWriteDependency ();

    	void CompleteWriteDependency ();
    	void CompleteReadDependency ();

    	void AddWriteDependency (JobHandle handle);
    	void AddReadDependency (JobHandle handle);

		void AddElements (GameObject srcGameObject, NativeSlice<int> outComponentIndices);
		void AddElements (int srcElementIndex, NativeSlice<int> outComponentIndices);
		void RemoveElement (int componentIndex);
    }

    //@TODO: This should be fully implemented in C++ for efficiency
    public class ComponentDataManager<T> : ScriptBehaviourManager, IComponentDataManager where T : struct, IComponentData
    {
    	internal NativeFreeList<T>                          m_Data;
        NativeList<JobHandle>                      	        m_Readers;
        JobHandle                                           m_Writer;
		static readonly int MaxPendingReaders = 16;

        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            m_Data = new NativeFreeList<T>(Allocator.Persistent);
			m_Data.Capacity = capacity;
			m_Readers = new NativeList<JobHandle>(16, Allocator.Persistent);
        }

    	protected override void OnDestroyManager()
    	{
    		base.OnDestroyManager();

			CompleteForWriting ();
			m_Readers.Dispose();
    		m_Data.Dispose();
    	}

		public void AddElements (GameObject sourceGameObject, NativeSlice<int> outComponentIndices)
    	{
			CompleteForWriting ();

			var value = sourceGameObject.GetComponent<ComponentDataWrapper<T> > ().Value;

			m_Data.Add (value, outComponentIndices);
    	}

		public void AddElements (int srcElementIndex, NativeSlice<int> outComponentIndices)
		{
			CompleteForWriting ();

			var value = m_Data[srcElementIndex];

			m_Data.Add (value, outComponentIndices);
		}


		public int AddElement(T value)
		{
			CompleteForWriting ();
			return m_Data.Add (value);
		}

		public void RemoveElement (int componentIndex)
    	{
			CompleteForWriting ();
			m_Data.Remove (componentIndex);
    	}

    	public JobHandle GetReadDependency()
    	{
			if (m_Readers.Length == 0)
				return new JobHandle();
			if (m_Readers.Length > 1)
			{
				var combinedHandle = JobHandle.CombineDependencies(m_Readers);
				m_Readers.Clear();
				m_Readers.Add(combinedHandle);
			}
			return m_Readers[0];
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
			for (int i = 0; i < m_Readers.Length; ++i)
				m_Readers[i].Complete();
			m_Readers.Clear();
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
			if (m_Readers.Length >= MaxPendingReaders)
			{
				var combinedHandle = JobHandle.CombineDependencies(m_Readers);
				m_Readers.Clear();
				m_Readers.Add(combinedHandle);
			}
			m_Readers.Add(handle);
    	}
    }
}