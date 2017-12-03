using System;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;

namespace UnityEngine.ECS
{
    public abstract class ComponentSystemBase : ScriptBehaviourManager
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
	    
        protected ComponentSystemBase()
        {
            m_EntityManager = World.Active.GetOrCreateManager<EntityManager>();
        }

    	protected override void OnCreateManagerInternal(int capacity)
    	{
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

	    protected override void OnDestroyManager()
    	{
		    CompleteDependencyInternal();
		    UpdateInjectedComponentGroups();

		    if (null != m_InjectedComponentGroups)
		    {
			    for (int i = 0; i != m_InjectedComponentGroups.Length; i++)
			    {
				    if (m_InjectedComponentGroups[i] != null)
					    m_InjectedComponentGroups[i].Dispose();
			    }
                m_InjectedComponentGroups = null;
		    }

		    for (int i = 0; i != m_ComponentGroups.Length; i++)
		    {
			    if (m_ComponentGroups[i] != null)
				    m_ComponentGroups[i].Dispose();
		    }

		    m_CachedComponentGroupEnumerables = null;
			m_JobDependencyForReadingManagers = null;
			m_JobDependencyForWritingManagers = null;
    	}

        protected EntityManager EntityManager { get { return m_EntityManager; }  }

        // TODO: this should be made part of UnityEngine?
        static void ArrayUtilityAdd<T>(ref T[] array, T item)
        {
            System.Array.Resize(ref array, array.Length + 1);
            array[array.Length - 1] = item;
        }

	    public ComponentGroupArray<T> GetEntities<T>() where T : struct
	    {
		    for (int i = 0; i != m_CachedComponentGroupEnumerables.Length; i++)
		    {
			    var enumerable = m_CachedComponentGroupEnumerables[i] as ComponentGroupArray<T>;
			    if (enumerable != null)
				    return enumerable;
		    }

		    var res = new ComponentGroupArray<T>(EntityManager);

		    ArrayUtilityAdd(ref m_CachedComponentGroupEnumerables, res);
		    ArrayUtilityAdd(ref m_ComponentGroups, res.ComponentGroup);
		    
		    RecalculateTypesFromComponentGroups();

		    return res;
	    }

	    public void UpdateInjectedComponentGroups()
	    {
		    if (null == m_InjectedComponentGroups)
			    return;

    		foreach (var group in m_InjectedComponentGroups)
			    group.UpdateInjection (this);
		    
		    InjectFromEntityData.UpdateInjection(this, EntityManager, m_InjectedFromEntity);
    	}

        internal unsafe void CompleteDependencyInternal()
        {
	        fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
	        {
		        m_SafetyManager.CompleteDependencies(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length);
	        }
        }
    }

	public abstract class ComponentSystem : ComponentSystemBase
	{
		private NativeList<JobHandle> m_PreviousFrameDependencies;

	    internal sealed override void InternalUpdate()
	    {
		    CompleteDependencyInternal();
		    UpdateInjectedComponentGroups ();

		    OnUpdate();
		    
		    JobHandle.ScheduleBatchedJobs();
	    }

		protected sealed override void OnCreateManagerInternal(int capacity)
		{
			base.OnCreateManagerInternal(capacity);
		}

		/// <summary>
		/// Called once per frame on the main thread.
		/// </summary>
		protected abstract void OnUpdate();
	}

	public abstract class JobComponentSystem : ComponentSystemBase
	{
		private NativeList<JobHandle> m_PreviousFrameDependencies;

		internal sealed override void InternalUpdate()
		{
			UpdateInjectedComponentGroups();

			// We need to wait on all previous frame dependencies, otherwise it is possible that we create infinitely long dependency chains
			// without anyone ever waiting on it
			JobHandle.CompleteAll(m_PreviousFrameDependencies);
			m_PreviousFrameDependencies.Clear();

			JobHandle input = GetDependency();
			JobHandle output = OnUpdate(input);
			
			JobHandle.ScheduleBatchedJobs();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (JobsUtility.JobDebuggerEnabled)
			{
				// Check that all reading and writing jobs are a dependency of the output job, to
				// catch systems that forget to add one of their jobs to the dependency graph.
				//
				// Note that this check is not strictly needed as we would catch the mistake anyway later,
				// but checking it here means we can flag the system that has the mistake, rather than some
				// other (innocent) system that is doing things correctly.

				try
				{
					for (int i = 0; i < m_JobDependencyForReadingManagers.Length; ++i)
					{
						CheckJobDependencies(m_JobDependencyForReadingManagers[i], output);
					}

					for (int i = 0; i < m_JobDependencyForWritingManagers.Length; ++i)
					{
						CheckJobDependencies(m_JobDependencyForWritingManagers[i], output);
					}
				}
				catch (InvalidOperationException)
				{
					EmergencySyncAllJobs();
					throw;
				}
			}
#endif
			AddDependencyInternal(output);
		}

		override sealed protected void OnCreateManagerInternal(int capacity)
		{
			base.OnCreateManagerInternal(capacity);
			m_PreviousFrameDependencies = new NativeList<JobHandle>(1, Allocator.Persistent);
		}

		override protected void OnDestroyManager()
		{
			base.OnDestroyManager();
			m_PreviousFrameDependencies.Dispose();
		}

	    protected virtual JobHandle OnUpdate(JobHandle inputDeps)
	    {
		    return inputDeps;
	    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		private void CheckJobDependencies(int type, JobHandle returnedHandle)
		{
            AtomicSafetyHandle h = m_SafetyManager.GetSafetyHandle(type, true);

            unsafe
            {
                int readerCount = AtomicSafetyHandle.GetReaderArray(h, 0, IntPtr.Zero);
                JobHandle* readers = stackalloc JobHandle[readerCount];
                AtomicSafetyHandle.GetReaderArray(h, readerCount, (IntPtr) readers);

                for (int i = 0; i < readerCount; ++i)
                {
                    if (!JobHandle.CheckFenceIsDependencyOrDidSyncFence(readers[i], returnedHandle))
                    {
                        throw new InvalidOperationException($"The system {this.GetType()} reads {TypeManager.GetType(type)} via {AtomicSafetyHandle.GetReaderName(h, i)} but that type was not returned as a job dependency. To ensure correct behavior of other systems, the job or a dependency of it must be returned from the OnUpdate method.");
                    }
                }

                JobHandle writer = AtomicSafetyHandle.GetWriter(h);
                if (!JobHandle.CheckFenceIsDependencyOrDidSyncFence(writer, returnedHandle))
                {
                    throw new InvalidOperationException($"The system {this.GetType()} writes {TypeManager.GetType(type)} via {AtomicSafetyHandle.GetWriterName(h)} but that was not returned as a job dependency. To ensure correct behavior of other systems, the job or a dependency of it must be returned from the OnUpdate method.");
                }
            }
		}

		private void EmergencySyncAllJobs()
		{
            for (int i = 0; i < m_JobDependencyForReadingManagers.Length; ++i)
            {
                int type = m_JobDependencyForReadingManagers[i];
                AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_SafetyManager.GetSafetyHandle(type, true));
            }

            for (int i = 0; i < m_JobDependencyForWritingManagers.Length; ++i)
            {
                int type = m_JobDependencyForWritingManagers[i];
                AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_SafetyManager.GetSafetyHandle(type, true));
            }
		}
#endif

		unsafe JobHandle GetDependency ()
		{
			fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
			{
				return m_SafetyManager.GetDependency(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length);
			}
		}

		protected unsafe void AddDependencyInternal(JobHandle dependency)
		{
			m_PreviousFrameDependencies.Add(dependency);
			
			fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
			{
				m_SafetyManager.AddDependency(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length, dependency);
			}
		}
	}

}
