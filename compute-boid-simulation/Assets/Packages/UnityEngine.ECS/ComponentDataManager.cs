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
		void RemoveElements (NativeArray<int> elements);
    }

    //@TODO: This should be fully implemented in C++ for efficiency
    public class ComponentDataManager<T> : ScriptBehaviourManager, IComponentDataManager where T : struct, IComponentData
    {
    	internal NativeFreeList<T>                          m_Data;
        NativeList<JobHandle>                      	        m_ReadersList;
        JobHandle                                           m_Readers;
        JobHandle                                           m_Writer;

        protected override void OnCreateManager(int capacity)
        {
            base.OnCreateManager(capacity);

            m_Data = new NativeFreeList<T>(Allocator.Persistent);
			m_Data.Capacity = capacity;
			m_ReadersList = new NativeList<JobHandle>(Allocator.Persistent);
        }

    	protected override void OnDestroyManager()
    	{
    		base.OnDestroyManager();

			CompleteForWriting ();
			m_ReadersList.Dispose();
    		m_Data.Dispose();
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

		public void RemoveElements (NativeArray<int> elements)
    	{
			CompleteForWriting ();

			for (int i = 0; i < elements.Length; i++)
				m_Data.Remove (elements[i]);
    	}

    	public JobHandle GetReadDependency()
    	{
			if (m_ReadersList.Length > 0)
			{
				// @TODO: this would be much better if it created a single combined dependency instead of a chain
				for (int i = 0; i < m_ReadersList.Length; ++i)
				{
					if (m_ReadersList[i].isDone)
						m_ReadersList[i].Complete();
					else if (m_Readers.isDone)
					{
						m_Readers.Complete();
						m_Readers = m_ReadersList[i];
					}
					else
						m_Readers = JobHandle.CombineDependencies(m_Readers, m_ReadersList[i]);
				}
				m_ReadersList.Clear();
			}
			return m_Readers;
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
			m_Readers.Complete();
			for (int i = 0; i < m_ReadersList.Length; ++i)
				m_ReadersList[i].Complete();
			m_ReadersList.Clear();
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
			m_ReadersList.Add(handle);
    	}
    }
}