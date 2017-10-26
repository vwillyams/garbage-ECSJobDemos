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
		public TupleSystem[] Tuples { get { return m_Tuples; } }
        TupleSystem[] 					m_Tuples;

        internal ComponentType[]		    m_JobDependencyForReadingManagers;
        internal ComponentType[]		    m_JobDependencyForWritingManagers;
        protected ComponentJobSafetyManager m_SafetyManager;

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

            m_SafetyManager = DependencyManager.GetBehaviourManager<EntityManager>().ComponentJobSafetyManager;
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

    	override public void OnUpdate()
    	{
			OnUpdateDontCompleteDependencies ();

            CompleteDependencyInternal();
    	}

        internal void CompleteDependencyInternal()
        {
            foreach (var dep in m_JobDependencyForReadingManagers)
                m_SafetyManager.CompleteWriteDependency(dep.typeIndex);
            foreach (var dep in m_JobDependencyForWritingManagers)
            {
                m_SafetyManager.CompleteWriteDependency(dep.typeIndex);
                m_SafetyManager.CompleteReadDependency(dep.typeIndex);
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
		NativeList<JobHandle>                   m_JobDependencyCombineList;

    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);
			m_JobDependencyCombineList = new NativeList<JobHandle>(Allocator.Persistent);
    	}

    	override protected void OnDestroyManager()
    	{
    		base.OnDestroyManager();
			m_JobDependencyCombineList.Dispose();
    	}
		
		override public void OnUpdate()
		{
			OnUpdateDontCompleteDependencies ();
		}

		public JobHandle GetDependency ()
		{
			int maxDependencyLength = m_JobDependencyForReadingManagers.Length + m_JobDependencyForWritingManagers.Length * 2;
			if (m_JobDependencyCombineList.Capacity < maxDependencyLength)
				m_JobDependencyCombineList.Capacity = maxDependencyLength;
			m_JobDependencyCombineList.Clear();
			foreach (var dep in m_JobDependencyForReadingManagers)
			{
				m_JobDependencyCombineList.Add(m_SafetyManager.GetWriteDependency (dep.typeIndex));
			}
			foreach (var dep in m_JobDependencyForWritingManagers)
			{
				m_JobDependencyCombineList.Add(m_SafetyManager.GetWriteDependency (dep.typeIndex));
				m_JobDependencyCombineList.Add(m_SafetyManager.GetReadDependency (dep.typeIndex));
			}
			return JobHandle.CombineDependencies(m_JobDependencyCombineList);
		}

		public void CompleteDependency ()
		{
            CompleteDependencyInternal();
		}

		public void AddDependency (JobHandle handle)
		{
			foreach (var dep in m_JobDependencyForReadingManagers)
				m_SafetyManager.AddReadDependency (dep.typeIndex, handle);
			foreach (var dep in m_JobDependencyForWritingManagers)
				m_SafetyManager.AddWriteDependency (dep.typeIndex, handle);
		}
	}

}