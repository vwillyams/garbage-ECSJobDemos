using System;
using Unity.Jobs;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.ECS;

namespace Unity.ECS
{
    public abstract class ComponentSystemBase : ScriptBehaviourManager
    {
        private InjectComponentGroupData[] 			m_InjectedComponentGroups;
        private InjectFromEntityData                m_InjectFromEntityData;

        private ComponentGroupArrayStaticCache[] 	m_CachedComponentGroupArrays;
        private ComponentGroup[] 				    m_ComponentGroups;


        internal int[]		    			m_JobDependencyForReadingManagers;
        internal int[]		    			m_JobDependencyForWritingManagers;
        internal ComponentJobSafetyManager  m_SafetyManager;
        private EntityManager                       m_EntityManager;
        private World                               m_World;

        public ComponentGroup[] 			ComponentGroups => m_ComponentGroups;

        protected override void OnCreateManagerInternal(World world, int capacity)
        {
            m_World = world;
            m_EntityManager = world.GetOrCreateManager<EntityManager>();
            m_SafetyManager = m_EntityManager.ComponentJobSafetyManager;

            ComponentSystemInjection.Inject(this, world, m_EntityManager, out m_InjectedComponentGroups, out m_InjectFromEntityData);

            m_ComponentGroups = new ComponentGroup[m_InjectedComponentGroups.Length];
            for (var i = 0; i < m_InjectedComponentGroups.Length; ++i)
                m_ComponentGroups [i] = m_InjectedComponentGroups[i].EntityGroup;

            m_CachedComponentGroupArrays = new ComponentGroupArrayStaticCache[0];

            RecalculateTypesFromComponentGroups();

            UpdateInjectedComponentGroups();
        }

        private void RecalculateTypesFromComponentGroups()
        {
            var readingTypes = new List<int>();
            var writingTypes = new List<int>();

            ComponentGroup.ExtractJobDependencyTypes(ComponentGroups, readingTypes, writingTypes);
            m_InjectFromEntityData.ExtractJobDependencyTypes(readingTypes, writingTypes);

            m_JobDependencyForReadingManagers = readingTypes.ToArray();
            m_JobDependencyForWritingManagers = writingTypes.ToArray();
        }

        protected sealed override void OnAfterDestroyManagerInternal()
        {
            if (null != m_InjectedComponentGroups)
            {
                for (var i = 0; i != m_InjectedComponentGroups.Length; i++)
                {
                    if (m_InjectedComponentGroups[i] != null)
                        m_InjectedComponentGroups[i].Dispose();
                }
                m_InjectedComponentGroups = null;
            }

            if (null != m_CachedComponentGroupArrays)
            {
                for (var i = 0; i != m_CachedComponentGroupArrays.Length; i++)
                {
                    if (m_CachedComponentGroupArrays[i] != null)
                        m_CachedComponentGroupArrays[i].Dispose();
                }
                m_CachedComponentGroupArrays = null;
            }

            for (var i = 0; i != m_ComponentGroups.Length; i++)
            {
                if (m_ComponentGroups[i] != null)
                    m_ComponentGroups[i].Dispose();
            }

            m_JobDependencyForReadingManagers = null;
            m_JobDependencyForWritingManagers = null;
        }

        protected override void OnBeforeDestroyManagerInternal()
        {
            CompleteDependencyInternal();
            UpdateInjectedComponentGroups();
        }

        protected EntityManager EntityManager => m_EntityManager;
        protected World World => m_World;

        // TODO: this should be made part of UnityEngine?
        private static void ArrayUtilityAdd<T>(ref T[] array, T item)
        {
            Array.Resize(ref array, array.Length + 1);
            array[array.Length - 1] = item;
        }

        public ComponentGroupArray<T> GetEntities<T>() where T : struct
        {
            for (var i = 0; i != m_CachedComponentGroupArrays.Length; i++)
            {
                if (m_CachedComponentGroupArrays[i].CachedType == typeof(T))
                    return new ComponentGroupArray<T>(m_CachedComponentGroupArrays[i]);
            }

            var cache = new ComponentGroupArrayStaticCache(typeof(T), EntityManager);

            ArrayUtilityAdd(ref m_CachedComponentGroupArrays, cache );
            ArrayUtilityAdd(ref m_ComponentGroups, cache.ComponentGroup);

            RecalculateTypesFromComponentGroups();

            return new ComponentGroupArray<T>(cache);
        }

        public unsafe void UpdateInjectedComponentGroups()
        {
            if (null == m_InjectedComponentGroups)
                return;

            ulong gchandle;
            var pinnedSystemPtr = (byte*)UnsafeUtility.PinGCObjectAndGetAddress(this, out gchandle);

            try
            {
                foreach (var group in m_InjectedComponentGroups)
                    group.UpdateInjection (pinnedSystemPtr);

                m_InjectFromEntityData.UpdateInjection(pinnedSystemPtr, EntityManager);
            }
            catch
            {
                UnsafeUtility.ReleaseGCObject(gchandle);
                throw;
            }
            UnsafeUtility.ReleaseGCObject(gchandle);
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
        private void BeforeOnUpdate()
        {
            CompleteDependencyInternal();
            UpdateInjectedComponentGroups ();
        }

        private void AfterOnUpdate()
        {
            JobHandle.ScheduleBatchedJobs();
        }

        internal sealed override void InternalUpdate()
        {
            BeforeOnUpdate();
            OnUpdate();
            AfterOnUpdate();
        }

        protected sealed override void OnCreateManagerInternal(World world, int capacity)
        {
            base.OnCreateManagerInternal(world, capacity);
        }

        protected sealed override void OnBeforeDestroyManagerInternal()
        {
            base.OnBeforeDestroyManagerInternal();
        }

        /// <summary>
        /// Called once per frame on the main thread.
        /// </summary>
        protected abstract void OnUpdate();
    }

    public abstract class JobComponentSystem : ComponentSystemBase
    {
        private JobHandle m_PreviousFrameDependency;

        private JobHandle BeforeOnUpdate()
        {
            UpdateInjectedComponentGroups();

            // We need to wait on all previous frame dependencies, otherwise it is possible that we create infinitely long dependency chains
            // without anyone ever waiting on it
            m_PreviousFrameDependency.Complete();

            return GetDependency();
        }

        private unsafe void AfterOnUpdate(JobHandle outputJob)
        {
            JobHandle.ScheduleBatchedJobs();

            AddDependencyInternal(outputJob);
            m_PreviousFrameDependency = outputJob;

#if ENABLE_UNITY_COLLECTIONS_CHECKS

            if (!JobsUtility.JobDebuggerEnabled)
                return;

            // Check that all reading and writing jobs are a dependency of the output job, to
            // catch systems that forget to add one of their jobs to the dependency graph.
            //
            // Note that this check is not strictly needed as we would catch the mistake anyway later,
            // but checking it here means we can flag the system that has the mistake, rather than some
            // other (innocent) system that is doing things correctly.

            try
            {
                for (var index = 0; index < m_JobDependencyForReadingManagers.Length; index++)
                {
                    var type = m_JobDependencyForReadingManagers[index];
                    var readerDependency = m_SafetyManager.GetDependency(&type, 1, null, 0);
                    CheckJobDependencies(type, true, readerDependency);

                    var writerDependency = m_SafetyManager.GetDependency(null, 0, &type, 1);
                    CheckJobDependencies(type, false, writerDependency);
                }

                for (var index = 0; index < m_JobDependencyForWritingManagers.Length; index++)
                {
                    var type = m_JobDependencyForWritingManagers[index];
                    var readerDependency = m_SafetyManager.GetDependency(&type, 1, null, 0);
                    CheckJobDependencies(type, true, readerDependency);

                    var writerDependency = m_SafetyManager.GetDependency(null, 0, &type, 1);
                    CheckJobDependencies(type, false, writerDependency);
                }
            }
            catch (InvalidOperationException)
            {
                EmergencySyncAllJobs();
                throw;
            }
#endif
        }

        internal sealed override void InternalUpdate()
        {
            var inputJob = BeforeOnUpdate();

            var outputJob = OnUpdate(inputJob);

            AfterOnUpdate(outputJob);
        }

        protected sealed override void OnCreateManagerInternal(World world, int capacity)
        {
            base.OnCreateManagerInternal(world, capacity);
        }

        protected sealed override void OnBeforeDestroyManagerInternal()
        {
            base.OnBeforeDestroyManagerInternal();
            m_PreviousFrameDependency.Complete();
        }

        protected virtual JobHandle OnUpdate(JobHandle inputDeps)
        {
            return inputDeps;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private void CheckJobDependencies(int type, bool isReading, JobHandle dependency)
        {
            var h = m_SafetyManager.GetSafetyHandle(type, true);

            unsafe
            {
                if (!isReading)
                {
                    var readerCount = AtomicSafetyHandle.GetReaderArray(h, 0, IntPtr.Zero);
                    JobHandle* readers = stackalloc JobHandle[readerCount];
                    AtomicSafetyHandle.GetReaderArray(h, readerCount, (IntPtr) readers);

                    for (var i = 0; i < readerCount; ++i)
                    {
                        if (!JobHandle.CheckFenceIsDependencyOrDidSyncFence(readers[i], dependency))
                        {
                            throw new InvalidOperationException($"The system {GetType()} reads {TypeManager.GetType(type)} via {AtomicSafetyHandle.GetReaderName(h, i)} but that type was not returned as a job dependency. To ensure correct behavior of other systems, the job or a dependency of it must be returned from the OnUpdate method.");
                        }
                    }
                }

                var writer = AtomicSafetyHandle.GetWriter(h);
                if (!JobHandle.CheckFenceIsDependencyOrDidSyncFence(writer, dependency))
                {
                    throw new InvalidOperationException($"The system {GetType()} writes {TypeManager.GetType(type)} via {AtomicSafetyHandle.GetWriterName(h)} but that was not returned as a job dependency. To ensure correct behavior of other systems, the job or a dependency of it must be returned from the OnUpdate method.");
                }
            }
        }

        private void EmergencySyncAllJobs()
        {
            foreach (var type in m_JobDependencyForReadingManagers)
            {
                AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_SafetyManager.GetSafetyHandle(type, true));
            }

            foreach (var type in m_JobDependencyForWritingManagers)
            {
                AtomicSafetyHandle.EnforceAllBufferJobsHaveCompleted(m_SafetyManager.GetSafetyHandle(type, true));
            }
        }
#endif

        private unsafe JobHandle GetDependency ()
        {
            fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
            {
                return m_SafetyManager.GetDependency(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length);
            }
        }

        private unsafe void AddDependencyInternal(JobHandle dependency)
        {
            fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
            {
                m_SafetyManager.AddDependency(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length, dependency);
            }
        }
    }
}
