using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using UnityEditor;

namespace UnityEngine.ECS
{
    public abstract class ComponentSystem : ScriptBehaviourManager
    {
	    InjectComponentGroupData[] 			m_InjectedComponentGroups;
	    InjectionData[] 					m_InjectedFromEntity;

	    object[] 							m_CachedComponentGroupEnumerables;
	    ComponentGroup[] 				    m_ComponentGroups;


        internal int[]		    			m_JobDependencyForReadingManagers;
        internal int[]		    			m_JobDependencyForWritingManagers;
        internal ComponentJobSafetyManager  m_SafetyManager;
        EntityManager                       m_EntityManager;

	    
	    public ComponentGroup[] 			ComponentGroups { get { return m_ComponentGroups; } }
	    
        protected ComponentSystem()
        {
            m_EntityManager = DependencyManager.GetBehaviourManager<EntityManager>();
        }

    	override protected void OnCreateManager(int capacity)
    	{
    		base.OnCreateManager(capacity);

            m_SafetyManager = m_EntityManager.ComponentJobSafetyManager;

		    ComponentSystemInjection.Inject(GetType(), m_EntityManager, out m_InjectedComponentGroups, out m_InjectedFromEntity);
		    
		    m_ComponentGroups = new ComponentGroup[m_InjectedComponentGroups.Length];
		    for (var i = 0; i < m_InjectedComponentGroups.Length; ++i)
			    m_ComponentGroups [i] = m_InjectedComponentGroups[i].EntityGroup;

		    m_CachedComponentGroupEnumerables = new object[0];
		    
		    RecalculateTypesFromComponentGroups();

		    UpdateInjectedComponentGroups();
	    }

	    void RecalculateTypesFromComponentGroups()
	    {
		    var readingTypes = new List<int>();
		    var writingTypes = new List<int>();
		    
		    ComponentGroup.ExtractJobDependencyTypes(ComponentGroups, readingTypes, writingTypes);
		    InjectFromEntityData.ExtractJobDependencyTypes(m_InjectedFromEntity, readingTypes, writingTypes);
		    
		    m_JobDependencyForReadingManagers = readingTypes.ToArray();
		    m_JobDependencyForWritingManagers = writingTypes.ToArray();
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

		    for (int i = 0; i != m_ComponentGroups.Length; i++)
		    {
			    if (m_ComponentGroups[i] != null)
				    m_ComponentGroups[i].Dispose();
		    }

		    m_CachedComponentGroupEnumerables = null;
    		m_InjectedComponentGroups = null;
			m_JobDependencyForReadingManagers = null;
			m_JobDependencyForWritingManagers = null;
    	}

        protected EntityManager EntityManager { get { return m_EntityManager; }  }

	    public ComponentGroupEnumerable<T> GetEntities<T>() where T : struct
	    {
		    for (int i = 0; i != m_CachedComponentGroupEnumerables.Length; i++)
		    {
			    var enumerable = m_CachedComponentGroupEnumerables[i] as ComponentGroupEnumerable<T>;
			    if (enumerable != null)
				    return enumerable;
		    }

		    var res = new ComponentGroupEnumerable<T>(EntityManager);
		    ArrayUtility.Add(ref m_CachedComponentGroupEnumerables, res);
		    ArrayUtility.Add(ref m_ComponentGroups, res.ComponentGroup);
		    
		    RecalculateTypesFromComponentGroups();

		    return res;
	    }

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