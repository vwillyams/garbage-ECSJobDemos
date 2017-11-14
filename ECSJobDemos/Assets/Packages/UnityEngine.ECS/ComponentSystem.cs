﻿using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine.ECS
{
    public abstract class ComponentSystem : ScriptBehaviourManager
    {
        TupleSystem[] 					m_Tuples;

        public ComponentGroup[] ComponentGroups
        {
			get
            {
				var groupArray = new ComponentGroup[m_Tuples.Length];
				for (var i = 0; i < groupArray.Length; ++i)
					groupArray[i] = m_Tuples[i].EntityGroup;
				return groupArray;
			}
		}

        internal int[]		    			m_JobDependencyForReadingManagers;
        internal int[]		    			m_JobDependencyForWritingManagers;
        internal ComponentJobSafetyManager  m_SafetyManager;
        EntityManager                       m_EntityManager;

        protected ComponentSystem()
        {
            m_EntityManager = DependencyManager.GetBehaviourManager<EntityManager>();
        }

    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);

            m_SafetyManager = m_EntityManager.ComponentJobSafetyManager;
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

        protected EntityManager EntityManager { get { return m_EntityManager; }  }

    	protected void UpdateInjectedTuples()
    	{
    		foreach (var tuple in m_Tuples)
    			tuple.UpdateInjection (this);
    	}

    	override public void OnUpdate()
    	{
			OnUpdateDontCompleteDependencies ();

            CompleteDependencyInternal();
    	}

        internal unsafe void CompleteDependencyInternal()
        {
	        fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
	        {
		        m_SafetyManager.CompleteDependencies(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length);
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

		public unsafe JobHandle GetDependency ()
		{
			fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
			{
				return m_SafetyManager.GetDependency(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length);
			}
		}

		public void CompleteDependency ()
		{
            CompleteDependencyInternal();
		}

		public unsafe void AddDependency (JobHandle dependency)
		{
			fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
			{
				m_SafetyManager.AddDependency(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length, dependency);
			}
		}
	}

}