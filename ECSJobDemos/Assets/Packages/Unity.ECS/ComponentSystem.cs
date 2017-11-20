﻿using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;

namespace UnityEngine.ECS
{
    public abstract class ComponentSystem : ScriptBehaviourManager
    {
	    InjectComponentGroupData[] 			m_InjectedComponentGroups;
	    InjectionData[] 					m_InjectedFromEntity;

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

		    ComponentSystemInjection.Inject(GetType(), m_EntityManager, out m_InjectedComponentGroups, out m_InjectedFromEntity);

		    var readingTypes = new List<int>();
		    var writingTypes = new List<int>();
		    
		    ComponentGroup.ExtractJobDependencyTypes(ComponentGroups, readingTypes, writingTypes);
		    InjectFromEntityData.ExtractJobDependencyTypes(m_InjectedFromEntity, readingTypes, writingTypes);
		    m_JobDependencyForReadingManagers = readingTypes.ToArray();
		    m_JobDependencyForWritingManagers = writingTypes.ToArray();
		    
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
		    
		    InjectFromEntityData.UpdateInjection(this, EntityManager, m_InjectedFromEntity);
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
		private NativeList<JobHandle> m_PreviousFrameDependencies;
		
		override protected void OnCreateManager(int capacity)
		{
			base.OnCreateManager(capacity);
			m_PreviousFrameDependencies = new NativeList<JobHandle>(1, Allocator.Persistent);
		}

		override protected void OnDestroyManager()
		{
			base.OnDestroyManager();
			m_PreviousFrameDependencies.Dispose();
		}
		
		
  		override public void OnUpdate()
		{
			OnUpdateFromJobComponentSystem();
			
			// We need to wait on all previous frame dependencies, otherwise it is possible that we create infinitely long dependency chains
			// without anyone ever waiting on it
			JobHandle.CompleteAll(m_PreviousFrameDependencies);
			m_PreviousFrameDependencies.Clear();
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
			m_PreviousFrameDependencies.Add(dependency);
			
			fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
			{
				m_SafetyManager.AddDependency(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length, dependency);
			}
		}
	}

}