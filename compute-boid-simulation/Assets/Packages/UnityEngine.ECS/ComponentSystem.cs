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
    public abstract class ComponentSystem : ScriptBehaviourManager
    {
        TupleSystem[] 							m_Tuples;
        internal IComponentDataManager[]		m_JobDependencyForReadingManagers;
        internal IComponentDataManager[]		m_JobDependencyForWritingManagers;

    	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    	static void Initialize()
    	{
			foreach (var ass in AppDomain.CurrentDomain.GetAssemblies ())
			{
				var types = ass.GetTypes().Where(t => t.IsSubclassOf(typeof(ComponentSystem)) && !t.IsAbstract);
				foreach (var type in types)
					DependencyManager.GetBehaviourManager (type);	
			}
    	}
    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);
			InjectTuples.CreateTuplesInjection (GetType(), this, out m_Tuples, out m_JobDependencyForReadingManagers, out m_JobDependencyForWritingManagers);
    	}

    	override protected void OnDestroyManager()
    	{
    		base.OnDestroyManager();
    		for (int i = 0; i != m_Tuples.Length; i++)
    		{
    			if (m_Tuples[i] != null)
    				m_Tuples[i].Dispose ();
    		}
    		m_Tuples = null;
			m_JobDependencyForReadingManagers = null;
			m_JobDependencyForWritingManagers = null;
    	}

    	protected void UpdateInjectedTuples()
    	{
    		foreach (var tuple in m_Tuples)
    			InjectTuples.UpdateInjection (tuple, this);
    	}

    	override protected void OnUpdate()
    	{
			OnUpdateDontCompleteDependencies ();

			foreach (var dep in m_JobDependencyForReadingManagers)
				dep.CompleteWriteDependency ();
			foreach (var dep in m_JobDependencyForWritingManagers)
			{
				dep.CompleteWriteDependency ();
				dep.CompleteReadDependency ();
			}
    	}

		internal void OnUpdateDontCompleteDependencies()
		{
			base.OnUpdate ();
			UpdateInjectedTuples ();
		}
    }

	public abstract class JobComponentSystem : ComponentSystem
	{
		override protected void OnUpdate()
		{
			OnUpdateDontCompleteDependencies ();
		}

		public JobHandle GetDependency ()
		{
			JobHandle handle = new JobHandle();
			foreach (var dep in m_JobDependencyForReadingManagers)
			{
				var newHandle = dep.GetWriteDependency ();
				if (newHandle.isDone)
					newHandle.Complete();
				else if (handle.isDone)
				{
					handle.Complete();
					handle = newHandle;
				}
				else
				{
					//@TODO: should combine all handles at once, not as a chain
					handle = JobHandle.CombineDependencies(handle, newHandle);
				}
			}
			foreach (var dep in m_JobDependencyForWritingManagers)
			{
				var newHandle = dep.GetWriteDependency ();
				if (newHandle.isDone)
					newHandle.Complete();
				else if (handle.isDone)
				{
					handle.Complete();
					handle = newHandle;
				}
				else
				{
					//@TODO: should combine all handles at once, not as a chain
					handle = JobHandle.CombineDependencies(handle, newHandle);
				}
				newHandle = dep.GetReadDependency ();
				if (newHandle.isDone)
					newHandle.Complete();
				else if (handle.isDone)
				{
					handle.Complete();
					handle = newHandle;
				}
				else
				{
					//@TODO: should combine all handles at once, not as a chain
					handle = JobHandle.CombineDependencies(handle, newHandle);
				}
			}
			return handle;
		}

		public void CompleteDependency ()
		{
			foreach (var dep in m_JobDependencyForReadingManagers)
				dep.CompleteWriteDependency ();
			foreach (var dep in m_JobDependencyForWritingManagers)
			{
				dep.CompleteWriteDependency ();
				dep.CompleteReadDependency ();
			}
		}

		public void AddDependency (JobHandle handle)
		{
			foreach (var dep in m_JobDependencyForReadingManagers)
				dep.AddReadDependency (handle);
			foreach (var dep in m_JobDependencyForWritingManagers)
				dep.AddWriteDependency (handle);
		}
	}

}