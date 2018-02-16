using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.ECS
{
    public abstract class ComponentSystemBase : ScriptBehaviourManager
    {
        InjectComponentGroupData[] 			m_InjectedComponentGroups;
        InjectFromEntityData                m_InjectFromEntityData;

        ComponentGroupArrayStaticCache[] 	m_CachedComponentGroupArrays;
        ComponentGroup[] 				    m_ComponentGroups;


        internal int[]		    			m_JobDependencyForReadingManagers;
        internal int[]		    			m_JobDependencyForWritingManagers;
        internal ComponentJobSafetyManager  m_SafetyManager;
        EntityManager                       m_EntityManager;
        World                               m_World;

        public ComponentGroup[] 			ComponentGroups => m_ComponentGroups;

        protected override void OnCreateManagerInternal(World world, int capacity)
        {
            m_World = world;
            m_EntityManager = world.GetOrCreateManager<EntityManager>();
            m_SafetyManager = m_EntityManager.ComponentJobSafetyManager;

            m_ComponentGroups = new ComponentGroup[0];
            m_CachedComponentGroupArrays = new ComponentGroupArrayStaticCache[0];

            ComponentSystemInjection.Inject(this, world, m_EntityManager, out m_InjectedComponentGroups, out m_InjectFromEntityData);

            RecalculateTypesFromComponentGroups();
            UpdateInjectedComponentGroups();
        }

        void RecalculateTypesFromComponentGroups()
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
            m_InjectedComponentGroups = null;
            m_CachedComponentGroupArrays = null;

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
        static void ArrayUtilityAdd<T>(ref T[] array, T item)
        {
            Array.Resize(ref array, array.Length + 1);
            array[array.Length - 1] = item;
        }

        unsafe internal ComponentGroup GetComponentGroup(ComponentType* componentTypes, int count)
        {
            for (var i = 0; i != m_ComponentGroups.Length; i++)
            {
                if (m_ComponentGroups[i].CompareComponents(componentTypes, count))
                    return m_ComponentGroups[i];
            }

            var group = EntityManager.CreateComponentGroup(componentTypes, count);
            ArrayUtilityAdd(ref m_ComponentGroups, group);

            RecalculateTypesFromComponentGroups();

            return group;
        }

        unsafe public ComponentGroup GetComponentGroup(params ComponentType[] componentTypes)
        {
            fixed (ComponentType* typesPtr = componentTypes)
            {
                return GetComponentGroup(typesPtr, componentTypes.Length);
            }
        }

        public ComponentGroupArray<T> GetEntities<T>() where T : struct
        {
            for (var i = 0; i != m_CachedComponentGroupArrays.Length; i++)
            {
                if (m_CachedComponentGroupArrays[i].CachedType == typeof(T))
                    return new ComponentGroupArray<T>(m_CachedComponentGroupArrays[i]);
            }

            var cache = new ComponentGroupArrayStaticCache(typeof(T), EntityManager, this);
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
        void BeforeOnUpdate()
        {
            CompleteDependencyInternal();
            UpdateInjectedComponentGroups ();
        }

        void AfterOnUpdate()
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
        JobHandle m_PreviousFrameDependency;

        JobHandle BeforeOnUpdate()
        {
            UpdateInjectedComponentGroups();

            // We need to wait on all previous frame dependencies, otherwise it is possible that we create infinitely long dependency chains
            // without anyone ever waiting on it
            m_PreviousFrameDependency.Complete();

            return GetDependency();
        }

        unsafe void AfterOnUpdate(JobHandle outputJob)
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
        void CheckJobDependencies(int type, bool isReading, JobHandle dependency)
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

        void EmergencySyncAllJobs()
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

        unsafe JobHandle GetDependency ()
        {
            fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
            {
                return m_SafetyManager.GetDependency(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length);
            }
        }

        unsafe void AddDependencyInternal(JobHandle dependency)
        {
            fixed (int* readersPtr = m_JobDependencyForReadingManagers, writersPtr = m_JobDependencyForWritingManagers)
            {
                m_SafetyManager.AddDependency(readersPtr, m_JobDependencyForReadingManagers.Length, writersPtr, m_JobDependencyForWritingManagers.Length, dependency);
            }
        }
    }
}
