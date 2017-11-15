using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine.ECS
{
    public abstract class ComponentSystem : ScriptBehaviourManager
    {
        InjectComponentGroupData[] 					m_InjectedComponentGroups;

        public ComponentGroup[] ComponentGroups
        {
			get
            {
				var groupArray = new ComponentGroup[m_InjectedComponentGroups.Length];
				for (var i = 0; i < groupArray.Length; ++i)
					groupArray[i] = m_InjectedComponentGroups[i].EntityGroup;
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

		    m_InjectedComponentGroups = InjectComponentGroupData.InjectComponentGroups(GetType(), m_EntityManager);
		    ComponentGroup.ExtractJobDependencyTypes(ComponentGroups, out m_JobDependencyForReadingManagers, out m_JobDependencyForWritingManagers);

		    UpdateInjectedComponentGroups();
	    }

    	override protected void OnDestroyManager()
    	{
    		base.OnDestroyManager();
		    
		    CompleteDependencyInternal();
		    UpdateInjectedComponentGroups();
		    
    		for (int i = 0; i != m_InjectedComponentGroups.Length; i++)
    		{
    			if (m_InjectedComponentGroups[i] != null)
    				m_InjectedComponentGroups[i].Dispose ();
    		}
    		m_InjectedComponentGroups = null;
			m_JobDependencyForReadingManagers = null;
			m_JobDependencyForWritingManagers = null;
    	}

        protected EntityManager EntityManager { get { return m_EntityManager; }  }

    	protected void UpdateInjectedComponentGroups()
    	{
    		foreach (var group in m_InjectedComponentGroups)
			    group.UpdateInjection (this);
    	}

    	override public void OnUpdate()
    	{
		    base.OnUpdate ();

		    CompleteDependencyInternal();
		    UpdateInjectedComponentGroups ();
	  	}
	    
	    
	    internal void OnUpdateFromJobComponentSystem()
	    {
		    base.OnUpdate ();
		    UpdateInjectedComponentGroups ();
	    }

        internal unsafe void CompleteDependencyInternal()
        {
	        fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
	        {
		        m_SafetyManager.CompleteDependencies(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length);
	        }
        }

    }

	public abstract class JobComponentSystem : ComponentSystem
	{
  		override public void OnUpdate()
		{
			OnUpdateFromJobComponentSystem();
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