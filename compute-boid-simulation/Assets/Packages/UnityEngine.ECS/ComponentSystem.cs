using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine.ECS
{
    public abstract class ComponentSystem : ScriptBehaviourManager
    {
        TupleSystem[] 					m_Tuples;

        //@TODO:
        public ComponentGroup[]              ComponentGroups { get { return new ComponentGroup[0]; } }

        internal ComponentType[]		    m_JobDependencyForReadingManagers;
        internal ComponentType[]		    m_JobDependencyForWritingManagers;
        protected ComponentJobSafetyManager m_SafetyManager;
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